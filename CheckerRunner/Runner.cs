using System.Diagnostics;

Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
string[] arguments = Environment.GetCommandLineArgs();
string filePath;
string userId;
string checkerType;

if (arguments.Length != 4)
{
    return;
}

filePath = arguments[1];
userId = arguments[2];
checkerType = arguments[3];

string tempFolderPath = Environment.CurrentDirectory + "/temp/";
string checkerBinPath = Environment.CurrentDirectory + "/checker_bin/";
string folderName = tempFolderPath + DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss") + "_u_" + userId + "/";

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