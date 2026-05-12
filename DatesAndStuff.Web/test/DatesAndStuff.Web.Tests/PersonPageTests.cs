using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace DatesAndStuff.Web.Tests;

[TestFixture]
public class PersonPageTests
{
    private IWebDriver driver;
    private StringBuilder verificationErrors;
    private const string BaseURL = "http://localhost:5091";
    private bool acceptNextAlert = true;

    private Process? _blazorProcess;

    [OneTimeSetUp]
    public void StartBlazorServer()
    {
        var webProjectPath = Path.GetFullPath(Path.Combine(
            Assembly.GetExecutingAssembly().Location,
            "../../../../../../src/DatesAndStuff.Web/DatesAndStuff.Web.csproj"
            ));

        var webProjFolderPath = Path.GetDirectoryName(webProjectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = @"C:\Program Files\dotnet\dotnet.exe",
            Arguments = $"run --project \"{webProjectPath}\"",
            //Arguments = "dotnet run --no-build",
            WorkingDirectory = webProjFolderPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _blazorProcess = Process.Start(startInfo);

        // Wait for the app to become available
        var client = new HttpClient();
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.Now;

        while (DateTime.Now - start < timeout)
        {
            try
            {
                var result = client.GetAsync(BaseURL).Result;
                if (result.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                Thread.Sleep(1000);
            }
        }
    }

    [OneTimeTearDown]
    public void StopBlazorServer()
    {
        if (_blazorProcess != null && !_blazorProcess.HasExited)
        {
            _blazorProcess.Kill(true);
            _blazorProcess.Dispose();
        }
    }

    [SetUp]
    public void SetupTest()
    {
        driver = new ChromeDriver();
        verificationErrors = new StringBuilder();
    }

    [TearDown]
    public void TeardownTest()
    {
        try
        {
            driver.Quit();
            driver.Dispose();
        }
        catch (Exception)
        {
            // Ignore errors if unable to close the browser
        }
        Assert.That(verificationErrors.ToString(), Is.EqualTo(""));
    }

    [TestCase(0, 5000)]
    [TestCase(5, 5250)]
    [TestCase(10, 5500)]
    public void Person_SalaryIncrease_ShouldIncrease(double salaryIncreasePercentage, double expectedSalary)
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);
        driver.FindElement(By.XPath("//*[@data-test='PersonPageNavigation']")).Click();

        Thread.Sleep(500);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

        var input = wait.Until(ExpectedConditions.ElementExists(By.XPath("//*[@data-test='SalaryIncreasePercentageInput']")));
        input.Clear();
        input.SendKeys(salaryIncreasePercentage.ToString());

        // Act
        var submitButton = wait.Until(ExpectedConditions.ElementExists(By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']")));
        submitButton.Click();


        // Assert
        var salaryLabel = wait.Until(ExpectedConditions.ElementExists(By.XPath("//*[@data-test='DisplayedSalary']")));
        var salaryAfterSubmission = double.Parse(salaryLabel.Text);
        salaryAfterSubmission.Should().BeApproximately(expectedSalary, 0.001);
    }

    [Test]
    public void Person_SalaryIncrease_BelowMinus10_ShouldShowValidationErrors()
    {
        // Arrange
        driver.Navigate().GoToUrl(BaseURL);
        driver.FindElement(By.XPath("//*[@data-test='PersonPageNavigation']")).Click();

        Thread.Sleep(500);

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

        var input = wait.Until(ExpectedConditions.ElementExists(By.XPath("//*[@data-test='SalaryIncreasePercentageInput']")));
        input.Clear();
        input.SendKeys("-11");

        // Act
        var submitButton = wait.Until(ExpectedConditions.ElementExists(By.XPath("//*[@data-test='SalaryIncreaseSubmitButton']")));
        submitButton.Click();

        // Assert
        var fieldValidationMessage = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(".validation-message")));
        fieldValidationMessage.Text.Should().Contain("The specified percentag should be between -9 and infinity.");

        var summaryValidationMessage = wait.Until(ExpectedConditions.ElementExists(By.CssSelector(".validation-errors")));
        summaryValidationMessage.Text.Should().Contain("The specified percentag should be between -9 and infinity.");
    }

    [Test]
    public void BlazeDemo_MexicoCityToDublin_ShouldHaveAtLeastThreeFlights()
    {
        // Arrange
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        driver.Navigate().GoToUrl("https://blazedemo.com");

        driver.FindElement(By.XPath("//select[@name='fromPort']/option[@value='Mexico City']")).Click();
        driver.FindElement(By.XPath("//select[@name='toPort']/option[@value='Dublin']")).Click();

        // Act
        driver.FindElement(By.CssSelector("input[type='submit']")).Click();

        // Assert
        var flightRows = driver.FindElements(By.CssSelector("table.table tbody tr"));
        flightRows.Count.Should().BeGreaterThanOrEqualTo(3, "because there should be at least three flights between Mexico City and Dublin");

        const double thresholdPrice = 450.0;
        var foundCheapFlight = false;

        foreach (var row in flightRows)
        {
            var priceText = row.FindElement(By.XPath("./td[6]")).Text;

            if (double.TryParse(priceText.Replace("$", string.Empty), System.Globalization.CultureInfo.InvariantCulture, out var price) &&
                price < thresholdPrice)
            {
                foundCheapFlight = true;
                break;
            }
        }

        if (foundCheapFlight)
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var fileName = $"CheapFlight_Dublin_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(desktopPath, fileName);

            screenshot.SaveAsFile(filePath);
            Console.WriteLine($"Screenshot saved to: {filePath}");
        }
    }

    private bool IsElementPresent(By by)
    {
        try
        {
            driver.FindElement(by);
            return true;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    private bool IsAlertPresent()
    {
        try
        {
            driver.SwitchTo().Alert();
            return true;
        }
        catch (NoAlertPresentException)
        {
            return false;
        }
    }

    private string CloseAlertAndGetItsText()
    {
        try
        {
            IAlert alert = driver.SwitchTo().Alert();
            string alertText = alert.Text;
            if (acceptNextAlert)
            {
                alert.Accept();
            }
            else
            {
                alert.Dismiss();
            }
            return alertText;
        }
        finally
        {
            acceptNextAlert = true;
        }
    }
}