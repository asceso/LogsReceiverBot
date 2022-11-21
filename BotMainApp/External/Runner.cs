using Models.App;
using System;
using System.Diagnostics;
using System.IO;

namespace BotMainApp.External
{
    public static class Runner
    {
        /// <summary>
        /// Run dublicate checker
        /// </summary>
        /// <param name="filename">filename for check</param>
        /// <param name="userId">user id</param>
        /// <returns>json with path collection</returns>
        public static string RunDublicateChecker(string filename, long userId, ConfigModel config)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DublicateRemoveRunner.exe",
                Arguments = $"{filename} {userId} {config.CpanelRegex} {config.WhmRegex} {config.WebmailRegex}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process process = Process.Start(psi);
            process.WaitForExit();
            StreamReader reader = process.StandardOutput;
            string jsonResult = reader.ReadToEnd();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run checker app
        /// </summary>
        /// <param name="userId">user id</param>
        /// <param name="folderPath">main folder for check</param>
        /// <param name="cpanelFilepath">cpanel file</param>
        /// <param name="whmFilepath">whm file</param>
        /// <returns>json collection</returns>
        public static string RunCpanelChecker(long userId, string folderPath, string cpanelFilepath, string whmFilepath)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "CheckerRunner.exe",
                Arguments = $"{userId} {folderPath} {cpanelFilepath ?? "none"}  {whmFilepath ?? "none"}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process process = Process.Start(psi);
            process.WaitForExit();
            StreamReader reader = process.StandardOutput;
            string jsonResult = reader.ReadToEnd();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run database filler
        /// </summary>
        /// <param name="userId">user id</param>
        /// <param name="webmailFilePath">webmail filename</param>
        /// <param name="cpanelFilePath">cpanel filename</param>
        /// <param name="whmFilePath">whm filename</param>
        /// <returns>json result</returns>
        public static string RunDublicateFiller(long userId, string webmailFilePath, string cpanelFilePath, string whmFilePath)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DatabaseFillerApp.exe",
                Arguments = $"{userId} {webmailFilePath ?? "none"} {cpanelFilePath ?? "none"} {whmFilePath ?? "none"}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process process = Process.Start(psi);
            process.WaitForExit();
            StreamReader reader = process.StandardOutput;
            string jsonResult = reader.ReadToEnd();
            reader.Close();
            process.Close();
            return jsonResult;
        }
    }
}