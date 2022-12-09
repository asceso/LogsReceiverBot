using Newtonsoft.Json;
using OpenQA.Selenium;
using SeleniumExtensionLibrary;
using System.Text;

namespace DropMeLoader
{
    public class JsonAnswer
    {
        public string Filesize { get; set; }
        public string Unit { get; set; }
        public bool IsOnlyTxtFiles { get; set; }
    }

    public class Program
    {
        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string fileLink;
            bool checkOnlyTxtFile;
            string uploadFileToDirectory;
            string uploadFileSaveName;
            bool openForMakeProfile;

            if (arguments.Length < 6)
            {
                return;
            }

            fileLink = arguments[1];
            checkOnlyTxtFile = arguments[2] == "true";
            uploadFileToDirectory = arguments[3];
            uploadFileSaveName = arguments[4];
            openForMakeProfile = arguments[5] == "true";
            bool showOutput = arguments.Length > 6 && arguments[6] == "true";

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
            IWebDriver driver;
            if (checkOnlyTxtFile)
            {
                driver = executor.InitDriver(profile: new(Environment.CurrentDirectory + "/chrome_profile", "Selenium"));
            }
            else
            {
                driver = executor.InitDriver();
            }

            if (openForMakeProfile)
            {
                Console.WriteLine("Press anything when profile configured");
                Console.ReadKey();
                executor.CloseDriver(driver);
                return;
            }
            try
            {
                JsonAnswer answer = new();
                driver.GoToUrl(fileLink);
                if (checkOnlyTxtFile)
                {
                    IWebElement expandBtn = driver.FindElementOrGetNull(By.ClassName("expand"));
                    if (expandBtn == null)
                    {
                        throw new NotFoundException("not found expand button");
                    }
                    expandBtn.Click();

                    var files = driver.FindElements(By.XPath("//li[@class='fileDownload dragout']"));
                    answer.IsOnlyTxtFiles = true;
                    foreach (IWebElement fileInfo in files)
                    {
                        if (!string.IsNullOrEmpty(fileInfo.Text) && !fileInfo.Text.EndsWith(".txt"))
                        {
                            answer.IsOnlyTxtFiles = false;
                            break;
                        }
                    }

                    #region loading all txt

                    if (answer.IsOnlyTxtFiles)
                    {
                        bool isAllLoaded = false;
                        List<string> toLoadFilenames = new();
                        List<string> loadedFilenames = new();
                        foreach (IWebElement fileInfo in files)
                        {
                            toLoadFilenames.Add(fileInfo.Text.Split(Environment.NewLine).LastOrDefault());
                        }

                        using FileSystemWatcher watcher = new(Environment.CurrentDirectory + "/chrome_downloads");
                        watcher.EnableRaisingEvents = true;
                        watcher.Renamed += (s, e) =>
                        {
                            string loadedFilename = toLoadFilenames.FirstOrDefault(f => f == e.Name);
                            if (loadedFilename != null)
                            {
                                loadedFilenames.Add(loadedFilename);
                                if (toLoadFilenames.All(tf => loadedFilenames.Any(lf => lf == tf)))
                                {
                                    isAllLoaded = true;
                                }
                            }
                        };
                        if (answer.IsOnlyTxtFiles)
                        {
                            foreach (IWebElement fileInfo in files)
                            {
                                fileInfo.Click();
                            }
                        }

                        while (!isAllLoaded) ;
                        watcher.Dispose();

                        StringBuilder builder = new();
                        foreach (string filename in loadedFilenames)
                        {
                            string filepath = $"{Environment.CurrentDirectory}/chrome_downloads/{filename}";
                            using StreamReader reader = new(filepath);
                            builder.AppendLine(reader.ReadToEnd());
                            reader.Close();
                            File.Delete(filepath);
                        }
                        using StreamWriter writer = new(uploadFileToDirectory + "/" + uploadFileSaveName);
                        writer.Write(builder.ToString());
                        writer.Close();
                    }

                    #endregion loading all txt
                }

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
                else if (fileSizeElementText.ToLower().Contains('б'))
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
            Environment.Exit(0);
        }
    }
}