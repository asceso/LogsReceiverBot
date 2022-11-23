using Newtonsoft.Json;

namespace CheckerResultGrabber
{
    public class Program
    {
        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string folderName;

            if (arguments.Length < 2)
            {
                return;
            }

            folderName = arguments[1];
            bool showOutput = false;
            if (arguments.Length > 2)
            {
                showOutput = arguments[2] == "true";
            }

            try
            {
                JsonAnswer answer = new();
                DirectoryInfo mainCpanelDirectoryInfo = new(folderName + "/cpanel");
                DirectoryInfo mainWhmDirectoryInfo = new(folderName + "/whm");

                List<string> resultCpanelGoodList = new();
                List<string> resultCpanelBadList = new();
                List<string> resultWhmGoodList = new();
                List<string> resultWhmBadList = new();

                if (mainCpanelDirectoryInfo.Exists)
                {
                    DirectoryInfo[] directories = mainCpanelDirectoryInfo.GetDirectories();
                    foreach (var directoryInfo in directories)
                    {
                        await AppendFilesFromDirectoryAsync(resultCpanelGoodList, resultCpanelBadList, resultWhmGoodList, resultWhmBadList, directoryInfo);
                    }
                    Directory.Delete(mainCpanelDirectoryInfo.FullName, true);
                }
                if (mainWhmDirectoryInfo.Exists)
                {
                    DirectoryInfo[] directories = mainWhmDirectoryInfo.GetDirectories();
                    foreach (var directoryInfo in directories)
                    {
                        await AppendFilesFromDirectoryAsync(resultCpanelGoodList, resultCpanelBadList, resultWhmGoodList, resultWhmBadList, directoryInfo);
                    }
                    Directory.Delete(mainWhmDirectoryInfo.FullName, true);
                }

                answer.CpanelGood = await MakeFileFromListAsync(resultCpanelGoodList, folderName, "/cp_good.txt");
                answer.CpanelBad = await MakeFileFromListAsync(resultCpanelBadList, folderName, "/cp_bad.txt");
                answer.WhmGood = await MakeFileFromListAsync(resultWhmGoodList, folderName, "/whm_good.txt");
                answer.WhmBad = await MakeFileFromListAsync(resultWhmBadList, folderName, "/whm_bad.txt");
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

        private static async Task AppendFilesFromDirectoryAsync(List<string> cpGoodList, List<string> cpBadList, List<string> whmGoodList, List<string> whmBadList, DirectoryInfo directory)
        {
            string cpGoodFilename = directory.FullName + "/Result/cp_good.txt";
            string cpBadFilename = directory.FullName + "/Result/cp_bad.txt";
            string whmGoodFilename = directory.FullName + "/Result/whm_good.txt";
            string whmBadFilename = directory.FullName + "/Result/whm_bad.txt";

            await AppendFileToListAsync(cpGoodList, cpGoodFilename);
            await AppendFileToListAsync(cpBadList, cpBadFilename);
            await AppendFileToListAsync(whmGoodList, whmGoodFilename);
            await AppendFileToListAsync(whmBadList, whmBadFilename);
        }

        private static async Task AppendFileToListAsync(List<string> list, string filename)
        {
            if (File.Exists(filename))
            {
                string[] rows = await File.ReadAllLinesAsync(filename);
                foreach (var row in rows)
                {
                    list.Add(row);
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

        public class JsonAnswer
        {
            public string CpanelGood { get; set; }
            public string CpanelBad { get; set; }
            public string WhmGood { get; set; }
            public string WhmBad { get; set; }
        }
    }
}