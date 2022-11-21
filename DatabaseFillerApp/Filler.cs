using DataAdapter.Controllers;
using Extensions;
using Models.Database;
using Newtonsoft.Json;

namespace DatabaseFillerApp
{
    public class JsonAnswer
    {
        public int WebmailAddedCount { get; set; }
        public int CpanelAddedCount { get; set; }
        public int WhmAddedCount { get; set; }
    }

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

            if (arguments.Length < 5)
            {
                return;
            }

            userId = arguments[1];
            webmailFilePath = arguments[2];
            cpanelFilePath = arguments[3];
            whmFilePath = arguments[4];
            bool showOutput = arguments.Length > 5 && arguments[5] == "true";

            try
            {
                JsonAnswer answer = new();
                UserModel user = await UsersController.GetUserByIdAsync(long.Parse(userId));
                answer.WebmailAddedCount = await CheckFileAndPostLogsAsync(user, webmailFilePath, "webmail");
                answer.CpanelAddedCount = await CheckFileAndPostLogsAsync(user, cpanelFilePath, "cpanel");
                answer.WhmAddedCount = await CheckFileAndPostLogsAsync(user, whmFilePath, "whm");
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

        private static async Task<int> CheckFileAndPostLogsAsync(UserModel user, string filePath, string category)
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }
            using StreamReader reader = new(filePath);
            IEnumerable<string> rows = reader.ReadToEnd().Split(Environment.NewLine).Where(s => !s.IsNullOrEmpty());
            reader.Close();
            int addedCount = 0;
            foreach (var row in rows)
            {
                string[] parts = row.Split('|');
                if (parts.Length != 3)
                {
                    continue;
                }

                string url, login, password;
                url = parts[0];
                login = parts[1];
                password = parts[2];

                LogModel model = new()
                {
                    Url = url,
                    Login = login,
                    Password = password,
                    Category = category,
                    UploadedByUserId = user.Id,
                    UploadedByUsername = user.Username
                };
                if (await LogsController.PostLogAsync(model))
                {
                    addedCount++;
                }
            }
            return addedCount;
        }
    }
}