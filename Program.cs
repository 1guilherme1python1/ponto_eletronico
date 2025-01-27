using System.Drawing;
using System.Net;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SkiaSharp;
using Tesseract;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            string baseDirectory = AppContext.BaseDirectory;
            string caminhoArquivo = Path.Combine(baseDirectory, "dias_uteis.txt");
            string[] diasUteis = File.ReadAllText(caminhoArquivo).Split(',');

            int diaAtual = DateTime.Now.Day;

            if (Array.Exists(diasUteis, dia => dia.Trim() == diaAtual.ToString()))
            {
                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                using (IWebDriver driver = new ChromeDriver(options))
                {
                    driver.Navigate().GoToUrl("https://portalrh.mpac.mp.br/rhsysweb-portal/public/xcp/XcpLogin.xhtml");

                    string tessDataPath = @"D:\csharp\ponto-eletronico\lib";

                    Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessDataPath);

                    string projectRootPath = AppContext.BaseDirectory;

                    string captchaText = "";

                    var newCaptcha = driver.FindElement(By.Id("form:btnRefreshCaptcha"));

                    int attempts = 0;
                    int maxAttempts = 5;

                    do
                    {
                        var captchaElement = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                            .Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(
                                By.Id("form:imgDownloadCaptcha")));

                        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                        string screenshotPath = Path.Combine(projectRootPath, "screenshot.png");
                        screenshot.SaveAsFile(screenshotPath);

                        using (var fullScreenShot = new Bitmap(screenshotPath))
                        {
                            var location = captchaElement.Location;
                            var size = captchaElement.Size;

                            Rectangle captchaArea = new Rectangle(location.X, location.Y, size.Width, size.Height);

                            using (var captchaImage = fullScreenShot.Clone(captchaArea, fullScreenShot.PixelFormat))
                            {
                                string imagesCaptPath = Path.Combine(projectRootPath, "imagesCapt");
                                if (!Directory.Exists(imagesCaptPath))
                                {
                                    Directory.CreateDirectory(imagesCaptPath);
                                }

                                string captchaImagePath = Path.Combine(imagesCaptPath, "captcha_cropped.png");
                                captchaImage.Save(captchaImagePath, System.Drawing.Imaging.ImageFormat.Png);

                                Console.WriteLine($"Imagem do captcha salva em: {captchaImagePath}");

                                captchaText = ResolveCaptcha(captchaImagePath, tessDataPath);

                                if (!string.IsNullOrEmpty(captchaText))
                                {
                                    break;
                                }

                                Console.WriteLine($"Captcha text: {captchaText} tentativa em: ${attempts}");

                                Thread.Sleep(5000);
                                newCaptcha = driver.FindElement(By.Id("form:btnRefreshCaptcha"));
                                newCaptcha.Click();

                                attempts++;

                                if (attempts >= maxAttempts)
                                {
                                    throw new Exception("Falha ao resolver o captcha após várias tentativas.");
                                }
                            }
                        }
                    } while (string.IsNullOrEmpty(captchaText));


                    var username = driver.FindElement(By.Id("form:txtDesUsuario_c"));
                    var password = driver.FindElement(By.Id("form:txtDesSenha_c"));
                    var textCaptcha = driver.FindElement(By.Id("form:txtCaptcha"));

                    username.SendKeys("gvieira");
                    password.SendKeys("Volmatavs#1");
                    textCaptcha.SendKeys(captchaText);


                    var loginButton = driver.FindElement(By.Id("form:btn_login"));
                    Thread.Sleep(5000);
                    loginButton.Click();

                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                    // wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("form:btn_login"))).Click();

                    wait.Until(ExpectedConditions.UrlContains("pagina-logada"));

                    if (driver.Url.Contains("pagina-logada"))
                    {
                        Console.WriteLine("Login bem-sucedido!");
                    }
                    else
                    {
                        Console.WriteLine("Erro no login.");
                    }
                }
            }
            else
            {
                Console.WriteLine("Data fora dos dias uteis");
            }
        }
        catch (WebDriverTimeoutException e)
        {
            Console.WriteLine($"Erro de timeout: {e.Message}");
        }
    }
    
    static void CropCaptchaImage(string screenshotPath, string croppedPath, int x, int y, int width, int height)
    {
        using (var inputStream = File.OpenRead(screenshotPath))
        using (var originalImage = SKBitmap.Decode(inputStream))
        {
            var cropRect = new SKRectI(x, y, x + width, y + height);
            using (var croppedImage = new SKBitmap(cropRect.Width, cropRect.Height))
            using (var canvas = new SKCanvas(croppedImage))
            {
                canvas.DrawBitmap(originalImage, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
                canvas.Flush();

                using (var outputStream = File.OpenWrite(croppedPath))
                {
                    croppedImage.Encode(outputStream, SKEncodedImageFormat.Png, 100);
                }
            }
        }

        Console.WriteLine($"Imagem do captcha salva em: {croppedPath}");
    }

    static async Task<string> DownloadCaptchaImageAsync(string url)
    {
        string localPath = Path.Combine(Path.GetTempPath(), "captcha.png");

        using (var httpClient = new HttpClient())
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, imageBytes);
        }

        return localPath;
    }

    static string ResolveCaptcha(string imagePath, string tessDataPath)
    {
        try
        {
            using (var engine = new TesseractEngine(@tessDataPath, "eng", EngineMode.Default))
            {
                using (var img = Pix.LoadFromFile(imagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        return page.GetText().Trim();
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                try
                {
                    File.Delete(imagePath);
                    Console.WriteLine($"Imagem apagada: {imagePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao apagar a imagem: {ex.Message}");
                }
            }
        }
    }
}