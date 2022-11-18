using Newtonsoft.Json;
using System.Diagnostics;

Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string[] arguments = Environment.GetCommandLineArgs();
string folderName;
string filePath;
string userId;
string checkerType;

if (arguments.Length != 5)
{
    return;
}

folderName = arguments[1];
filePath = arguments[2];
userId = arguments[3];
checkerType = arguments[4];

try
{
    string tempFolderPath = Environment.CurrentDirectory + "/temp/";
    string checkerBinPath = Environment.CurrentDirectory + "/checker_bin/";

    if (!Directory.Exists(tempFolderPath)) Directory.CreateDirectory(tempFolderPath);
    Directory.CreateDirectory(folderName);
    foreach (string file in Directory.GetFiles(checkerBinPath))
    {
        FileInfo fi = new(file);
        File.Copy(file, folderName + fi.Name, true);
    }
    File.Copy(filePath, folderName + "/input.txt");

    ProcessStartInfo psi = new()
    {
        FileName = folderName + "checkcp.exe",
        WorkingDirectory = folderName,
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
    await Task.Delay(TimeSpan.FromSeconds(1));
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

    string cpanelGoodFilepath = folderName + "/Result/" + "cp_good.txt";
    string cpanelBadFilepath = folderName + "/Result/" + "cp_bad.txt";
    string whmGoodFilepath = folderName + "/Result/" + "whm_good.txt";
    string whmBadFilepath = folderName + "/Result/" + "whm_bad.txt";

    JsonAnswer answer = new()
    {
        CpanelGood = File.Exists(cpanelGoodFilepath) ? cpanelGoodFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        CpanelBad = File.Exists(cpanelBadFilepath) ? cpanelBadFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        WhmGood = File.Exists(whmGoodFilepath) ? whmGoodFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : "",
        WhmBad = File.Exists(whmBadFilepath) ? whmBadFilepath.Replace("\\", "/").Remove(2, 1).Insert(2, "\\") : ""
    };
    Console.WriteLine(JsonConvert.SerializeObject(answer));
}
catch (Exception ex)
{
    Console.WriteLine("{" + $"\"Error\":\"{ex.Message}\"" + "}");
}

public class JsonAnswer
{
    public string CpanelGood { get; set; }
    public string CpanelBad { get; set; }
    public string WhmGood { get; set; }
    public string WhmBad { get; set; }
}