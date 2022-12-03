using Newtonsoft.Json;
using OpenQA.Selenium;
using SeleniumExtensionLibrary;

namespace DropMeLoader
{
    public class JsonAnswer
    {
        public string Filesize { get; set; }
        public string Unit { get; set; }
    }

    public class Program
    {
        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string fileLink;

            if (arguments.Length < 2)
            {
                return;
            }

            fileLink = arguments[1];
            bool showOutput = arguments.Length > 2 && arguments[2] == "true";

            if (fileLink == "none")
            {
                Console.WriteLine("{" + $"\"Error\":\"link not exist\"" + "}");
                if (showOutput)
                {
                    Console.ReadKey();
                }
                return;
            }
            SeleniumExecutor executor = new();
            executor.SetConfig(new());
            IWebDriver driver = executor.InitDriver();

            try
            {
                JsonAnswer answer = new();
                driver.GoToUrl(fileLink);
                IWebElement fileSizeElement = driver.FindElementOrGetNull(By.ClassName("fileSize"));
                if (fileSizeElement == null)
                {
                    throw new NotFoundException("not found file size");
                }
                string fileSizeElementText = fileSizeElement.Text;
                if (fileSizeElementText.ToLower().Contains("гб"))
                {
                    answer.Unit = "GB";
                }
                else if (fileSizeElementText.ToLower().Contains("мб"))
                {
                    answer.Unit = "MB";
                }
                else if (fileSizeElementText.ToLower().Contains("кб"))
                {
                    answer.Unit = "KB";
                }
                else if (fileSizeElementText.ToLower().Contains("б"))
                {
                    answer.Unit = "B";
                }

                string fileSizeWithoutUnit = fileSizeElementText.Split(' ').FirstOrDefault().ToString();
                answer.Filesize = fileSizeWithoutUnit;

                Console.WriteLine(JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }
            finally
            {
                executor.CloseDriver(driver);
            }
            if (showOutput)
            {
                Console.ReadKey();
            }
        }
    }
}