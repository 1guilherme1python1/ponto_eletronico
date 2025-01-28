using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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
            string projectRootPath = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
            string baseDirectory = AppContext.BaseDirectory;
            string caminhoArquivo = Path.Combine(baseDirectory, "dias_uteis.txt");
            string[] diasUteis = File.ReadAllText(caminhoArquivo).Split(',');

            int diaAtual = DateTime.Now.Day;

            if (diasUteis.Contains(diaAtual.ToString()))
            {
                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                using (IWebDriver driver = new ChromeDriver(options))
                {
                    driver.Navigate().GoToUrl("https://portalrh.mpac.mp.br/rhsysweb-portal/public/xcp/XcpLogin.xhtml");

                    string tessDataPath =  @"/home/guilherme/Documentos/ponto_eletronico/lib/";
                    Environment.SetEnvironmentVariable("TESSDATA_PREFIX", tessDataPath);

                    string captchaText = "";

                    var newCaptcha = driver.FindElement(By.Id("form:btnRefreshCaptcha"));
                    int attempts = 0;
                    const int maxAttempts = 5;

                    do
                    {
                        var captchaElement = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                            .Until(ExpectedConditions.ElementIsVisible(By.Id("form:imgDownloadCaptcha")));

                        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                        string screenshotPath = Path.Combine(projectRootPath, "screenshot.png");
                        screenshot.SaveAsFile(screenshotPath);

                        string imagesCaptPath = Path.Combine(projectRootPath, "imagesCapt");
                        if (!Directory.Exists(imagesCaptPath))
                        {
                            Directory.CreateDirectory(imagesCaptPath);
                        }

                        string captchaImagePath = Path.Combine(imagesCaptPath, "captcha_cropped.png");

                        // Cortar o captcha
                        CropCaptchaImage(screenshotPath, captchaImagePath, captchaElement.Location.X, captchaElement.Location.Y, captchaElement.Size.Width, captchaElement.Size.Height);

                        // Resolver o captcha
                        captchaText = ResolveCaptcha(captchaImagePath, tessDataPath);

                        if (!string.IsNullOrEmpty(captchaText))
                        {
                            break;
                        }

                        Console.WriteLine($"Captcha inválido na tentativa {attempts + 1}. Gerando um novo...");

                        newCaptcha.Click();
                        attempts++;

                        if (attempts >= maxAttempts)
                        {
                            throw new Exception("Falha ao resolver o captcha após várias tentativas.");
                        }
                    } while (string.IsNullOrEmpty(captchaText));

                    // Preencher formulário de login
                    driver.FindElement(By.Id("form:txtDesUsuario_c")).SendKeys("gvieira");
                    driver.FindElement(By.Id("form:txtDesSenha_c")).SendKeys("Volmatavs#1");
                    driver.FindElement(By.Id("form:txtCaptcha")).SendKeys(captchaText);

                    var loginButton = driver.FindElement(By.Id("form:btn_login"));
                    Thread.Sleep(5000);
                    loginButton.Click();

                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
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
                Console.WriteLine("Data fora dos dias úteis");
            }
        }
        catch (WebDriverTimeoutException e)
        {
            Console.WriteLine($"Erro de timeout: {e.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
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

    static string ResolveCaptcha(string imagePath, string tessDataPath)
    {
        try
        {
            using (var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default))
            using (var img = Pix.LoadFromFile(imagePath))
            using (var page = engine.Process(img))
            {
                return page.GetText().Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao resolver captcha: {ex.Message}");
            return string.Empty;
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
