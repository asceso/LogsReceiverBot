using DataAdapter.Controllers;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace DublicateRemoveRunner
{
    public class JsonAnswer
    {
        public string Dublicates { get; set; }
        public string Cpanel { get; set; }
        public string Whm { get; set; }
        public string Webmail { get; set; }
    }

    public class ExternalDublicateRemover
    {
        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string resultDirPath;
            string filename;
            string cpanelRegexFormat;
            string whmRegexFormat;
            string webmailRegexFormat;

            if (arguments.Length < 6)
            {
                return;
            }

            resultDirPath = arguments[1];
            filename = arguments[2];
            cpanelRegexFormat = arguments[3];
            whmRegexFormat = arguments[4];
            webmailRegexFormat = arguments[5];
            bool showOutput = arguments.Length > 6 && arguments[6] == "true";

            try
            {
                string tempFolderPath = Environment.CurrentDirectory + "/temp/";
                if (!Directory.Exists(tempFolderPath)) Directory.CreateDirectory(tempFolderPath);
                DirectoryInfo di = new(resultDirPath);

                string dublicateFilePath = di.FullName + "/dublicates.txt";
                string cpanelFilePath = di.FullName + "/cpanel.txt";
                string whmFilePath = di.FullName + "/whm.txt";
                string webmailFilePath = di.FullName + "/webmail.txt";

                Regex cpanelRegex = new(cpanelRegexFormat);
                Regex whmRegex = new(whmRegexFormat);
                Regex webmailRegex = new(webmailRegexFormat);
                using StreamReader reader = new(filename);

                List<string> dbLogsData = LogsController.GetLogsData();
                List<string> dublicateList = new();
                List<string> cpanelList = new();
                List<string> whmList = new();
                List<string> webmailList = new();

                while (!reader.EndOfStream)
                {
                    string log = reader.ReadLine().Trim();
                    string[] parts = log.Split('|');
                    if (parts.Length != 3)
                    {
                        continue;
                    }

                    string url, login, password;
                    url = parts[0];
                    login = parts[1];
                    password = parts[2];

                    if (dbLogsData.Any(db => db == log))
                    {
                        if (!dublicateList.Any(d => d == log))
                        {
                            dublicateList.Add(log);
                        }
                    }
                    else if (cpanelRegex.IsMatch(url))
                    {
                        if (!cpanelList.Any(d => d == log))
                        {
                            cpanelList.Add(log);
                        }
                    }
                    else if (whmRegex.IsMatch(url))
                    {
                        if (!whmList.Any(d => d == log))
                        {
                            whmList.Add(log);
                        }
                    }
                    else if (webmailRegex.IsMatch(url))
                    {
                        Uri uri = new(url);
                        string parsedLog = $"{uri.DnsSafeHost}|587|{login}|{password}";
                        if (dbLogsData.Any(db => db == parsedLog))
                        {
                            if (!dublicateList.Any(d => d == parsedLog))
                            {
                                dublicateList.Add(parsedLog);
                            }
                        }
                        else if (!webmailList.Any(d => d == parsedLog))
                        {
                            webmailList.Add(parsedLog);
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                reader.Close();

                MakeFileIfAnyExist(dublicateList, dublicateFilePath);
                MakeFileIfAnyExist(cpanelList, cpanelFilePath);
                MakeFileIfAnyExist(whmList, whmFilePath);
                MakeFileIfAnyExist(webmailList, webmailFilePath);

                static void MakeFileIfAnyExist(List<string> list, string filename)
                {
                    if (list.Any())
                    {
                        using TextWriter writer = new StreamWriter(filename);
                        foreach (var line in list)
                        {
                            writer.WriteLine(line);
                        }
                        writer.Close();
                    }
                }

                if (File.Exists(filename)) File.Delete(filename);
                JsonAnswer answer = new()
                {
                    Dublicates = File.Exists(dublicateFilePath) ? dublicateFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
                    Cpanel = File.Exists(cpanelFilePath) ? cpanelFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
                    Whm = File.Exists(whmFilePath) ? whmFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
                    Webmail = File.Exists(webmailFilePath) ? webmailFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
                };
                Console.WriteLine(JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }
            if (showOutput)
            {
                Console.ReadKey();
            }
        }
    }
}