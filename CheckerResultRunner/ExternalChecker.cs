using Extensions;
using System.Diagnostics;

namespace CheckerResultRunner
{
    public class ExternalChecker
    {
        private static int splitByCount = 1000;

        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string folderName;
            string cpanelFilePath;
            string whmFilePath;

            if (arguments.Length < 4)
            {
                return;
            }

            folderName = arguments[1];
            cpanelFilePath = arguments[2];
            whmFilePath = arguments[3];
            if (arguments.Length > 4)
            {
                bool isOtherThreadCount = int.TryParse(arguments[4], out int inThreadCount);
                if (isOtherThreadCount)
                {
                    splitByCount = inThreadCount;
                }
            }
            bool showOutput = false;
            if (arguments.Length > 5)
            {
                showOutput = arguments[5] == "true";
            }

            try
            {
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

                waitingTasks.Add(Task.Run(async () =>
                {
                    await CheckListAsync(mainCpanelList, folderName, "cpanel");
                }));
                waitingTasks.Add(Task.Run(async () =>
                {
                    await CheckListAsync(mainWhmList, folderName, "whm");
                }));
                await Task.WhenAll(waitingTasks);
                Console.WriteLine("{\"Info\":\"End\"}");
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

        private static async Task CheckListAsync(List<string> source, string mainFolderPath, string checkType)
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
                }));
            }
            await Task.WhenAll(waitingTasks);
        }
    }
}