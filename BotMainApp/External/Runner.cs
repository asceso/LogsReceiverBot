﻿using Extensions;
using Models.App;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BotMainApp.External
{
    public static class Runner
    {
        /// <summary>
        /// Run dublicate checker
        /// </summary>
        /// <param name="resultDirectoryPath">target directory for check</param>
        /// <param name="filename">filename for check</param>
        /// <param name="config">config with regex</param>
        /// <returns>json with path collection</returns>
        public static async Task<string> RunDublicateChecker(string resultDirectoryPath, string filename, ConfigModel config)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DublicateRemoveRunner.exe",
                Arguments =
                $"\"{resultDirectoryPath}\" " +
                $"\"{filename}\" " +
                $"\"{config.CpanelRegex}\" " +
                $"\"{config.WhmRegex}\" " +
                $"\"{config.WebmailRegex}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run checker app
        /// </summary>
        /// <param name="folderPath">main folder for check</param>
        /// <param name="cpanelFilepath">cpanel file</param>
        /// <param name="whmFilepath">whm file</param>
        /// <param name="maxForThread">max for thread count</param>
        /// <returns>json collection</returns>
        public static async Task<string> RunCpanelChecker(string folderPath, string cpanelFilepath, string whmFilepath, int maxForThread)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "CheckerRunner.exe",
                Arguments =
                $"\"{folderPath}\" " +
                $"\"{(cpanelFilepath.IsNullOrEmptyString() ? "none" : cpanelFilepath)}\" " +
                $"\"{(whmFilepath.IsNullOrEmptyString() ? "none" : whmFilepath)}\" " +
                $"\"{maxForThread}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            var processId = process.Id;
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run checker app
        /// </summary>
        /// <param name="cpanelFilepath">cpanel file</param>
        /// <param name="whmFilepath">whm file</param>
        /// <param name="folderPath">main folder for check</param>
        /// <param name="maxForThread">max for thread count</param>
        /// <returns>json collection</returns>
        public static async Task<string> RunOwnCpanelChecker(string cpanelFilepath, string whmFilepath, string folderPath, int maxForThread)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "CpanelChecker.exe",
                Arguments =
                $"\"{(cpanelFilepath.IsNullOrEmptyString() ? "none" : cpanelFilepath)}\" " +
                $"\"{(whmFilepath.IsNullOrEmptyString() ? "none" : whmFilepath)}\" " +
                $"\"{folderPath}\" " +
                $"\"{maxForThread}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
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
        public static async Task<string> RunDublicateFiller(long userId, string webmailFilePath, string cpanelFilePath, string whmFilePath, bool fillRecords)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DatabaseFillerApp.exe",
                Arguments =
                $"\"{userId}\" " +
                $"\"{(webmailFilePath.IsNullOrEmptyString() ? "none" : webmailFilePath)}\" " +
                $"\"{(cpanelFilePath.IsNullOrEmptyString() ? "none" : cpanelFilePath)}\" " +
                $"\"{(whmFilePath.IsNullOrEmptyString() ? "none" : whmFilePath)}\" " +
                $"\"{fillRecords.ToString().ToLower()}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run database filler
        /// </summary>
        /// <param name="userId">user id</param>
        /// <param name="cpanelFilePath">cpanel filename</param>
        /// <param name="whmFilePath">whm filename</param>
        /// <returns>json result</returns>
        public static async Task<string> RunValidFiller(long userId, string cpanelFilePath, string whmFilePath)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DatabaseValidFillerApp.exe",
                Arguments =
                $"\"{userId}\" " +
                $"\"{(cpanelFilePath.IsNullOrEmptyString() ? "none" : cpanelFilePath)}\" " +
                $"\"{(whmFilePath.IsNullOrEmptyString() ? "none" : whmFilePath)}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
            reader.Close();
            process.Close();
            return jsonResult;
        }

        /// <summary>
        /// Run filepath txt in notepad
        /// </summary>
        /// <param name="notepadPath">path to notepad</param>
        /// <param name="filepath">filename</param>
        /// <returns>true if opened without error</returns>
        public static bool RunTextFileInNotepad(string notepadPath, string filepath)
        {
            try
            {
                if (notepadPath == "")
                {
                    notepadPath = "notepad";
                }
                ProcessStartInfo psi = new()
                {
                    FileName = $"{notepadPath}",
                    Arguments = $"\"{filepath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process process = Process.Start(psi);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Run explorer with selected path
        /// </summary>
        /// <param name="folderPath">folder path</param>
        /// <returns>true if opened without error</returns>
        public static bool RunExplorerWithPath(string folderPath)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = $"explorer",
                    Arguments = $"\"{folderPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process process = Process.Start(psi);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Run chrome with selected link
        /// </summary>
        /// <param name="link">link</param>
        /// <returns>true if opened without error</returns>
        public static bool RunChromeWithLink(string link)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = link,
                    UseShellExecute = true
                };
                Process process = Process.Start(psi);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Run drop me files checker
        /// </summary>
        /// <param name="fileLink">file link</param>
        /// <returns>file size json array</returns>
        public static async Task<string> RunDropMeLinkChecker(string fileLink)
        {
            ProcessStartInfo psi = new()
            {
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = "DropMeLinkChecker.exe",
                Arguments = $"\"{(fileLink.IsNullOrEmptyString() ? "none" : fileLink)}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process process = Process.Start(psi);
            await process.WaitForExitAsync();
            StreamReader reader = process.StandardOutput;
            string jsonResult = await reader.ReadToEndAsync();
            reader.Close();
            process.Close();
            return jsonResult;
        }
    }
}