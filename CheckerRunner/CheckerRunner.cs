using Extensions;
using Newtonsoft.Json;
using System.Diagnostics;

Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string[] arguments = Environment.GetCommandLineArgs();
string folderName;
string filePath;
string userId;
string checkerType;

if (arguments.Length < 5)
{
    return;
}

folderName = arguments[1];
filePath = arguments[2];
userId = arguments[3];
checkerType = arguments[4];
bool showOutput = arguments.Length > 5 && arguments[5] == "true";
int threadCount = 10;

Dictionary<string, JsonAnswer> resultDictionary = new();
try
{
    #region split file to parts

    string checkerBinPath = Environment.CurrentDirectory + "/checker_bin/";
    List<string> fileData = File.ReadAllText(filePath).Split(Environment.NewLine).ToList();
    List<List<string>> partitions = fileData.Partitions(threadCount);

    foreach (List<string> part in partitions)
    {
        string partFolderPath = folderName + "part" + partitions.IndexOf(part) + "/";
        Directory.CreateDirectory(partFolderPath);
        foreach (string file in Directory.GetFiles(checkerBinPath))
        {
            FileInfo fi = new(file);
            File.Copy(file, partFolderPath + fi.Name, true);
        }
        File.WriteAllLines(partFolderPath + "/input.txt", part);
        resultDictionary.Add(partFolderPath, new());
    }

    #endregion split file to parts

    #region for all dictionary parts run script

    List<Task> waitingTasks = new();
    foreach (string key in resultDictionary.Keys)
    {
        waitingTasks.Add(Task.Run(async () =>
        {
            ProcessStartInfo psi = new()
            {
                FileName = key + "checkcp.exe",
                WorkingDirectory = key,
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
            switch (checkerType)
            {
                case "whm": await writer.WriteLineAsync("2"); break;
                case "cpanel" or "webmail": await writer.WriteLineAsync("1"); break;
                default: await writer.WriteLineAsync("1"); break;
            }

            await writer.WriteLineAsync("input.txt");
            writer.Close();

            await process.WaitForExitAsync();
            process.Close();

            string cpanelGoodFilepath = key + "/Result/" + "cp_good.txt";
            string cpanelBadFilepath = key + "/Result/" + "cp_bad.txt";
            string whmGoodFilepath = key + "/Result/" + "whm_good.txt";
            string whmBadFilepath = key + "/Result/" + "whm_bad.txt";

            resultDictionary[key].CpanelGood = File.Exists(cpanelGoodFilepath) ? cpanelGoodFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "";
            resultDictionary[key].CpanelBad = File.Exists(cpanelBadFilepath) ? cpanelBadFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "";
            resultDictionary[key].WhmGood = File.Exists(whmGoodFilepath) ? whmGoodFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "";
            resultDictionary[key].WhmBad = File.Exists(whmBadFilepath) ? whmBadFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "";
        }));
    }
    await Task.WhenAll(waitingTasks);

    List<string> cpanelGoodList = new();
    List<string> cpanelBadList = new();
    List<string> whmGoodList = new();
    List<string> whmBadList = new();

    foreach (var key in resultDictionary.Keys)
    {
        AppendFileIfExist(cpanelGoodList, resultDictionary[key].CpanelGood);
        AppendFileIfExist(cpanelBadList, resultDictionary[key].CpanelBad);
        AppendFileIfExist(whmGoodList, resultDictionary[key].WhmGood);
        AppendFileIfExist(whmBadList, resultDictionary[key].WhmBad);
        Directory.Delete(key, true);
    }
    void AppendFileIfExist(List<string> list, string filepath)
    {
        if (File.Exists(filepath))
        {
            string[] dataLines = File.ReadAllLines(filepath);
            foreach (string data in dataLines)
            {
                list.Add(data);
            }
        }
    }

    MakeFileIfAnyExist(cpanelGoodList, folderName + "cpanel_good.txt");
    MakeFileIfAnyExist(cpanelBadList, folderName + "cpanel_bad.txt");
    MakeFileIfAnyExist(whmGoodList, folderName + "whm_good.txt");
    MakeFileIfAnyExist(whmBadList, folderName + "whm_bad.txt");
    void MakeFileIfAnyExist(List<string> list, string filename)
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

    var cpanelGoodPath = folderName + "cpanel_good.txt";
    var cpanelBadPath = folderName + "cpanel_bad.txt";
    var whmGoodPath = folderName + "whm_good.txt";
    var whmBadPath = folderName + "whm_bad.txt";

    JsonAnswer answer = new()
    {
        CpanelGood = File.Exists(cpanelGoodPath) ? cpanelGoodPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        CpanelBad = File.Exists(cpanelBadPath) ? cpanelBadPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        WhmGood = File.Exists(whmGoodPath) ? whmGoodPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        WhmBad = File.Exists(whmBadPath) ? whmBadPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : ""
    };
    Console.WriteLine(JsonConvert.SerializeObject(answer));

    #endregion for all dictionary parts run script
}
catch (Exception ex)
{
    foreach (var key in resultDictionary.Keys) Directory.Delete(key, true);
    Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
}

if (showOutput)
{
    Console.ReadKey();
}

public class JsonAnswer
{
    public string CpanelGood { get; set; }
    public string CpanelBad { get; set; }
    public string WhmGood { get; set; }
    public string WhmBad { get; set; }
}