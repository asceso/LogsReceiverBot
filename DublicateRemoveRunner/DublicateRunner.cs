using DataAdapter.Controllers;
using Extensions;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string[] arguments = Environment.GetCommandLineArgs();
string filename;
string userId;
string urlRegexPattern;
string loginRegexPattern;
string passwordRegexPattern;

if (arguments.Length < 6)
{
    return;
}

filename = arguments[1];
userId = arguments[2];
urlRegexPattern = arguments[3];
loginRegexPattern = arguments[4];
passwordRegexPattern = arguments[5];
bool showOutput = arguments.Length > 6 && arguments[6] == "true";
int threadCount = 10;

try
{
    string tempFolderPath = Environment.CurrentDirectory + "/temp/";
    string resultFolderPath = tempFolderPath + $"u_{userId}_check_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";

    if (!Directory.Exists(tempFolderPath)) Directory.CreateDirectory(tempFolderPath);
    Directory.CreateDirectory(resultFolderPath);

    List<string> data = new();
    string uniqueFilePath = resultFolderPath + "unique.txt";
    string dublicateFilePath = resultFolderPath + "dublicates.txt";

    Regex urlRegex = new(urlRegexPattern);
    Regex loginRegex = new(loginRegexPattern);
    Regex passwordRegex = new(passwordRegexPattern);
    using StreamReader reader = new(tempFolderPath + filename);
    while (!reader.EndOfStream)
    {
        string log = reader.ReadLine();
        if (data.Contains(log)) continue;
        string[] parts = log.Split('|');
        if (parts.Length != 3)
        {
            continue;
        }
        string url, login, password;
        url = parts[0];
        login = parts[1];
        password = parts[2];

        if (!urlRegex.IsMatch(url) || !loginRegex.IsMatch(login) || !passwordRegex.IsMatch(password))
        {
            continue;
        }

        data.Add(log);
    }
    reader.Close();

    List<string> dbLogsData = await LogsController.GetLogsDataAsync();
    List<string> uniqueList = new();
    List<string> dublicateList = new();

    var chunks = data.Partitions(threadCount);
    List<Task> waitingTasks = new();
    foreach (var chunk in chunks)
    {
        waitingTasks.Add(Task.Run(() =>
        {
            foreach (string log in chunk)
            {
                if (dbLogsData.Any(l => l == log))
                {
                    dublicateList.Add(log);
                }
                else
                {
                    uniqueList.Add(log);
                }
            }
        }));
    }
    Task.WaitAll(waitingTasks.ToArray());
    MakeFileIfAnyExist(uniqueList, uniqueFilePath);
    MakeFileIfAnyExist(dublicateList, dublicateFilePath);
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

    if (File.Exists(tempFolderPath + filename)) File.Delete(tempFolderPath + filename);
    JsonAnswer answer = new()
    {
        FolderPath = resultFolderPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\"),
        Unique = File.Exists(uniqueFilePath) ? uniqueFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        Dublicates = File.Exists(dublicateFilePath) ? dublicateFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : ""
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

public class JsonAnswer
{
    public string FolderPath { get; set; }
    public string Unique { get; set; }
    public string Dublicates { get; set; }
}