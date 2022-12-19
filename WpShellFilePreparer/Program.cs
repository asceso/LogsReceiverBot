using DataAdapter.Controllers;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace WpShellFilePreparer
{
    public class Program
    {
        public class JsonAnswer
        {
            public string Dublicates { get; set; }
            public string Unique { get; set; }
        }

        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string resultDirPath;
            string sourceFilename;
            bool loadDbRecords;

            if (arguments.Length < 4)
            {
                return;
            }

            resultDirPath = arguments[1];
            sourceFilename = arguments[2];
            loadDbRecords = arguments.Length > 3 && arguments[3] == "true";
            bool showOutput = arguments.Length > 4 && arguments[4] == "true";

            try
            {
                FileInfo sourceFileInfo = new(sourceFilename);

                #region clear not http\s and null string, removed non asci

                List<string> clearRows = new();
                using StreamReader reader = new(sourceFilename);
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(line) && (line.Contains("http://") || line.Contains("https://")))
                    {
                        clearRows.Add(Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(line)));
                    }
                }
                reader.Close();

                #endregion clear not http\s and null string, removed non asci

                #region checking dublicates and format string

                List<string> dbLogsData = new();
                if (loadDbRecords)
                {
                    dbLogsData = DublicatesController.GetLogsDataByCategory("wp-login");
                }
                List<string> dublicateList = new();
                List<string> uniqueList = new();
                foreach (string row in clearRows)
                {
                    string[] parts = row.Split('|', ':');
                    string url = string.Empty, login = string.Empty, password = string.Empty;
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    if (parts.Length == 3)
                    {
                        url = parts[0];
                        login = parts[1];
                        password = parts[2];
                    }
                    else
                    {
                        url = string.Empty;
                        for (int i = 0; i < parts.Length - 2; i++)
                        {
                            url += parts[i] + (i == parts.Length - 3 ? "" : ":");
                        }
                        login = parts[^2];
                        password = parts[^1];
                    }

                    Regex clearUrlRegex = new(@"(http:\/\/|https:\/\/).*\/");
                    string cleanUrl = clearUrlRegex.Match(url).Value;
                    string inBaseFormat = $"{cleanUrl}wp-login.php#{login}@{password}";

                    if (dbLogsData.Any(db => db == inBaseFormat))
                    {
                        if (!dublicateList.Any(d => d == inBaseFormat))
                        {
                            dublicateList.Add(inBaseFormat);
                        }
                    }
                    else
                    {
                        uniqueList.Add(inBaseFormat);
                    }
                }

                #endregion checking dublicates and format string

                DirectoryInfo di = new(resultDirPath);
                string dublicateFilePath = di.FullName + "/dublicates.txt";
                string uniqueFilePath = di.FullName + "/unique.txt";

                MakeFileIfAnyExist(dublicateList, dublicateFilePath);
                MakeFileIfAnyExist(uniqueList, uniqueFilePath);

                static void MakeFileIfAnyExist(List<string> list, string filename)
                {
                    if (list.Any())
                    {
                        using TextWriter writer = new StreamWriter(filename);
                        for (int i = 0; i < list.Count; i++)
                        {
                            string line = list[i];
                            if (i == list.Count - 1)
                            {
                                writer.Write(line);
                            }
                            else
                            {
                                writer.WriteLine(line);
                            }
                        }
                        writer.Close();
                    }
                }

                JsonAnswer answer = new()
                {
                    Dublicates = File.Exists(dublicateFilePath) ? dublicateFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
                    Unique = File.Exists(uniqueFilePath) ? uniqueFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : ""
                };
                Console.WriteLine(JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }
            finally
            {
                if (File.Exists(sourceFilename)) File.Delete(sourceFilename);
            }
            if (showOutput)
            {
                Console.ReadKey();
            }
        }
    }
}