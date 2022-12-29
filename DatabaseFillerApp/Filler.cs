using DataAdapter.Controllers;
using Extensions;
using Models.Database;
using Newtonsoft.Json;

namespace DatabaseFillerApp
{
    public class ExternalFiller
    {
        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string userId;
            string webmailFilePath;
            string cpanelFilePath;
            string whmFilePath;
            string wploginFilePath;

            if (arguments.Length < 6)
            {
                return;
            }

            userId = arguments[1];
            webmailFilePath = arguments[2];
            cpanelFilePath = arguments[3];
            whmFilePath = arguments[4];
            wploginFilePath = arguments[5];
            bool fillRecords = arguments.Length > 6 && arguments[6] == "true";
            bool showOutput = arguments.Length > 7 && arguments[7] == "true";

            try
            {
                JsonAnswer answer = new();
                UserModel user = await UsersController.GetUserByIdAsync(long.Parse(userId));
                answer.WebmailAddedCount = await CheckFileAndPostLogsAsync(user, webmailFilePath, "webmail", fillRecords);
                answer.CpanelAddedCount = await CheckFileAndPostLogsAsync(user, cpanelFilePath, "cpanel", fillRecords);
                answer.WhmAddedCount = await CheckFileAndPostLogsAsync(user, whmFilePath, "whm", fillRecords);
                answer.WpLoginAddedCount = await CheckFileAndPostLogsAsync(user, wploginFilePath, "wp-login", fillRecords);
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

        private static async Task<int> CheckFileAndPostLogsAsync(UserModel user, string filePath, string category, bool fillRecords)
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }
            using StreamReader reader = new(filePath);
            IEnumerable<string> rows = reader.ReadToEnd().Split(Environment.NewLine).Where(s => !s.IsNullOrEmptyString());
            reader.Close();
            int addedCount = 0;
            foreach (var row in rows)
            {
                DublicateModel model;
                if (category == "wp-login")
                {
                    try
                    {
                        string toReplaceString = row;
                        string url, login, password;
                        url = toReplaceString.Remove(toReplaceString.IndexOf("#"));
                        toReplaceString = toReplaceString.Replace(url + "#", string.Empty);
                        login = toReplaceString.Remove(toReplaceString.IndexOf("@"));
                        toReplaceString = toReplaceString.Replace(login + "@", string.Empty);
                        password = toReplaceString;

                        model = new()
                        {
                            Url = url,
                            Login = login,
                            Password = password,
                            Category = category,
                            UploadedByUserId = user.Id,
                            UploadedByUsername = user.Username
                        };
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else
                {
                    string[] parts = row.Split('|');
                    if (category == "webmail")
                    {
                        if (parts.Length != 4)
                        {
                            continue;
                        }

                        string url, port, login, password;
                        url = parts[0];
                        port = parts[1];
                        login = parts[2];
                        password = parts[3];

                        model = new()
                        {
                            Url = url + "|" + port,
                            Login = login,
                            Password = password,
                            Category = category,
                            UploadedByUserId = user.Id,
                            UploadedByUsername = user.Username
                        };
                    }
                    else
                    {
                        if (parts.Length != 3)
                        {
                            continue;
                        }

                        string url, login, password;
                        url = parts[0];
                        login = parts[1];
                        password = parts[2];

                        model = new()
                        {
                            Url = url,
                            Login = login,
                            Password = password,
                            Category = category,
                            UploadedByUserId = user.Id,
                            UploadedByUsername = user.Username
                        };
                    }
                }
                if (fillRecords)
                {
                    if (await DublicatesController.PostLogAsync(model))
                    {
                        addedCount++;
                    }
                }
            }
            return addedCount;
        }
    }

    public class JsonAnswer
    {
        public int WebmailAddedCount { get; set; }
        public int CpanelAddedCount { get; set; }
        public int WhmAddedCount { get; set; }
        public int WpLoginAddedCount { get; set; }
    }
}