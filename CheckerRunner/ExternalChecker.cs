using Extensions;
using Newtonsoft.Json;
using System.Diagnostics;

namespace CheckerRunner
{
    public class ExternalChecker
    {
        private static int splitByCount = 1000;

        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string userId;
            string folderName;
            string cpanelFilePath;
            string whmFilePath;

            if (arguments.Length < 5)
            {
                return;
            }

            userId = arguments[1];
            folderName = arguments[2];
            cpanelFilePath = arguments[3];
            whmFilePath = arguments[4];
            if (arguments.Length > 5)
            {
                bool isOtherThreadCount = int.TryParse(arguments[5], out int inThreadCount);
                if (isOtherThreadCount)
                {
                    splitByCount = inThreadCount;
                }
            }
            bool showOutput = false;
            if (arguments.Length > 6)
            {
                showOutput = arguments.Length > 6 && arguments[6] == "true";
            }

            try
            {
                JsonAnswer answer = new();
                List<Task> waitingTasks = new();
                List<string> mainCpanelList = new();
                List<string> mainWhmList = new();

                if (File.Exists(cpanelFilePath))
                {
                    using StreamReader reader = new(cpanelFilePath);
                    string[] buffer = (await reader.ReadToEndAsync()).Split(Environment.NewLine).Where(s => !s.IsNullOrEmptyString()).ToArray();
                    reader.Close();
                    mainCpanelList.AddRange(buffer);
                }
                if (File.Exists(whmFilePath))
                {
                    using StreamReader reader = new(whmFilePath);
                    string[] buffer = (await reader.ReadToEndAsync()).Split(Environment.NewLine).Where(s => !s.IsNullOrEmptyString()).ToArray();
                    reader.Close();
                    mainWhmList.AddRange(buffer);
                }

                List<CheckerResponce> cpanelResponces = new();
                List<CheckerResponce> whmResponces = new();
                if (cpanelFilePath == "none" || !mainCpanelList.Any())
                {
                    answer.CpanelGood = "";
                    answer.CpanelBad = "";
                }
                else
                {
                    waitingTasks.Add(Task.Run(async () =>
                    {
                        cpanelResponces.Add(await CheckListAsync(mainCpanelList, folderName, "cpanel"));
                    }));
                }

                if (whmFilePath == "none" || !mainCpanelList.Any())
                {
                    answer.WhmGood = "";
                    answer.WhmBad = "";
                }
                else
                {
                    waitingTasks.Add(Task.Run(async () =>
                    {
                        whmResponces.Add(await CheckListAsync(mainWhmList, folderName, "whm"));
                    }));
                }
                await Task.WhenAll(waitingTasks);

                List<string> resultCpanelGoodList = new();
                List<string> resultCpanelBadList = new();
                List<string> resultWhmGoodList = new();
                List<string> resultWhmBadList = new();

                foreach (CheckerResponce responce in cpanelResponces) await CalculateResponceAndMakeListAsync(resultCpanelGoodList, resultCpanelBadList, responce);
                foreach (CheckerResponce responce in whmResponces) await CalculateResponceAndMakeListAsync(resultWhmGoodList, resultWhmBadList, responce);

                answer.CpanelGood = await MakeFileFromListAsync(resultCpanelGoodList, folderName, "cp_good.txt");
                answer.CpanelBad = await MakeFileFromListAsync(resultCpanelBadList, folderName, "cp_bad.txt");
                answer.WhmGood = await MakeFileFromListAsync(resultWhmGoodList, folderName, "whm_good.txt");
                answer.WhmBad = await MakeFileFromListAsync(resultWhmBadList, folderName, "whm_bad.txt");
                if (Directory.Exists(folderName + "cpanel/")) Directory.Delete(folderName + "cpanel/", true);
                if (Directory.Exists(folderName + "whm/")) Directory.Delete(folderName + "whm/", true);
                Console.WriteLine(JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                if (Directory.Exists(folderName + "cpanel/")) Directory.Delete(folderName + "cpanel/", true);
                if (Directory.Exists(folderName + "whm/")) Directory.Delete(folderName + "whm/", true);
                Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }

            if (showOutput)
            {
                Console.ReadKey();
            }
        }

        private static async Task<CheckerResponce> CheckListAsync(List<string> source, string mainFolderPath, string checkType)
        {
            int splitCount = source.Count / splitByCount;
            string checkerBinPath = Environment.CurrentDirectory + "/checker_bin/";
            Directory.CreateDirectory(mainFolderPath + checkType + "/");

            List<List<string>> partitions = source.Partitions(splitCount);
            List<string> folderForCheck = new();
            foreach (List<string> part in partitions)
            {
                string partFolderPath = mainFolderPath + checkType + "/part" + partitions.IndexOf(part) + "/";
                Directory.CreateDirectory(partFolderPath);
                foreach (string file in Directory.GetFiles(checkerBinPath))
                {
                    FileInfo fi = new(file);
                    File.Copy(file, partFolderPath + fi.Name, true);
                }
                File.WriteAllLines(partFolderPath + "/input.txt", part);
                folderForCheck.Add(partFolderPath);
            }

            List<Task> waitingTasks = new();
            CheckerResponce responce = new();
            responce.GoodFiles = new();
            responce.BadFiles = new();

            foreach (string folder in folderForCheck)
            {
                waitingTasks.Add(Task.Run(async () =>
                {
                    ProcessStartInfo psi = new()
                    {
                        FileName = folder + "checkcp.exe",
                        WorkingDirectory = folder,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process process = new()
                    {
                        StartInfo = psi
                    };
                    process.Start();

                    using StreamWriter writer = process.StandardInput;
                    switch (checkType)
                    {
                        case "cpanel": await writer.WriteLineAsync("1"); break;
                        case "whm": await writer.WriteLineAsync("2"); break;
                    }

                    await writer.WriteLineAsync("input.txt");
                    writer.Close();

                    await process.WaitForExitAsync();
                    process.Close();

                    string outGoodFilePath = string.Empty;
                    string outBadFilePath = string.Empty;
                    switch (checkType)
                    {
                        case "cpanel":
                            {
                                outGoodFilePath = folder + "Result/cp_good.txt";
                                outBadFilePath = folder + "Result/cp_bad.txt";
                            }
                            break;

                        case "whm":
                            {
                                outGoodFilePath = folder + "Result/whm_good.txt";
                                outBadFilePath = folder + "Result/whm_bad.txt";
                            }
                            break;
                    }

                    if (File.Exists(outGoodFilePath)) responce.GoodFiles.Add(outGoodFilePath);
                    if (File.Exists(outBadFilePath)) responce.BadFiles.Add(outBadFilePath);
                }));
            }
            await Task.WhenAll(waitingTasks);
            return responce;
        }

        private static async Task CalculateResponceAndMakeListAsync(List<string> listForGood, List<string> listForBad, CheckerResponce responce)
        {
            foreach (string filename in responce.GoodFiles)
            {
                if (File.Exists(filename))
                {
                    using StreamReader reader = new(filename);
                    listForGood.AddRange((await reader.ReadToEndAsync()).Split(Environment.NewLine).Where(s => !s.IsNullOrEmptyString()));
                }
            }
            foreach (string filename in responce.BadFiles)
            {
                if (File.Exists(filename))
                {
                    using StreamReader reader = new(filename);
                    listForBad.AddRange((await reader.ReadToEndAsync()).Split(Environment.NewLine).Where(s => !s.IsNullOrEmptyString()));
                }
            }
        }

        private static async Task<string> MakeFileFromListAsync(List<string> list, string foldername, string filename)
        {
            if (list.Any())
            {
                string filePath = foldername + filename;
                using TextWriter writer = new StreamWriter(filePath);
                foreach (var line in list)
                {
                    await writer.WriteLineAsync(line);
                }
                writer.Close();
                return filePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\");
            }
            else
            {
                return "";
            }
        }
    }

    public class JsonAnswer
    {
        public string CpanelGood { get; set; }
        public string CpanelBad { get; set; }
        public string WhmGood { get; set; }
        public string WhmBad { get; set; }
    }

    public class CheckerResponce
    {
        public List<string> GoodFiles { get; set; }
        public List<string> BadFiles { get; set; }
    }
}