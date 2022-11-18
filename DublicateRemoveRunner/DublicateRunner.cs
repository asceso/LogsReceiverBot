using DataAdapter.Controllers;
using Extensions;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

object syncObjectUnique = new();
object syncObjectDublicates = new();

Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string[] arguments = Environment.GetCommandLineArgs();
string filename;
string userId;
string urlRegexPattern;
string loginRegexPattern;
string passwordRegexPattern;

if (arguments.Length != 6)
{
    return;
}

filename = arguments[1];
userId = arguments[2];
urlRegexPattern = arguments[3];
loginRegexPattern = arguments[4];
passwordRegexPattern = arguments[5];

try
{
    string tempFolderPath = Environment.CurrentDirectory + "/temp/";
    string resultFolderPath = tempFolderPath + $"u_{userId}_check_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";

    if (!Directory.Exists(tempFolderPath)) Directory.CreateDirectory(tempFolderPath);
    Directory.CreateDirectory(resultFolderPath);

    List<string> data = new();
    string uniqueFilePath = resultFolderPath + "unique.txt";
    string dublicateFilePath = resultFolderPath + "dublicates.txt";

    var fs = File.Create(uniqueFilePath);
    fs.Close();
    fs = File.Create(dublicateFilePath);
    fs.Close();

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
    TextWriter writerUnique = new StreamWriter(uniqueFilePath);
    TextWriter writerDublicates = new StreamWriter(dublicateFilePath);

    var chunks = data.Partitions(20);
    List<Task> waitingTasks = new();
    foreach (var chunk in chunks)
    {
        waitingTasks.Add(Task.Run(() =>
        {
            foreach (string log in chunk)
            {
                if (dbLogsData.Any(l => l == log))
                {
                    SyncWriteDublicates(writerDublicates, log);
                }
                else
                {
                    SyncWriteUniques(writerUnique, log);
                }
            }
        }));
    }
    Task.WaitAll(waitingTasks.ToArray());
    writerUnique.Close();
    writerDublicates.Close();

    if (File.Exists(tempFolderPath + filename)) File.Delete(tempFolderPath + filename);
    JsonAnswer answer = new()
    {
        Unique = uniqueFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\"),
        Dublicates = dublicateFilePath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\"),
        FolderPath = resultFolderPath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\")
    };
    Console.WriteLine(JsonConvert.SerializeObject(answer));
}
catch (Exception ex)
{
    Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
}

void SyncWriteUniques(TextWriter writer, string data) => SyncWrite(syncObjectUnique, writer, data);
void SyncWriteDublicates(TextWriter writer, string data) => SyncWrite(syncObjectDublicates, writer, data);
void SyncWrite(object syncObject, TextWriter writer, string data)
{
    lock (syncObject)
    {
        writer.WriteLine(data);
        writer.Flush();
    }
}

public class JsonAnswer
{
    public string Unique { get; set; }
    public string Dublicates { get; set; }
    public string FolderPath { get; set; }
}