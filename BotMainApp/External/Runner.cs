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
                Arguments = $"{filename} {userId} {config.UrlRegex} {config.LoginRegex} {config.PasswordRegex}",
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
        /// Run cpanel checker
        /// </summary>
        /// <param name="folderPath">folder where cpanel will working</param>
        /// <param name="filename">filename for check</param>
        /// <param name="userId">user id</param>
        /// <param name="checkType">check type cpanel or whm</param>
        /// <returns>json with path collection</returns>
        public static string RunCpanelChecker(string folderPath, string filename, long userId, string checkType)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "CheckerRunner.exe",
                Arguments = $"{folderPath} {filename} {userId} {checkType}",
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