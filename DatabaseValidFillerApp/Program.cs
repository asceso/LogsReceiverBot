using DataAdapter.Controllers;
using Extensions;
using Models.Database;
using Newtonsoft.Json;

namespace DatabaseValidFillerApp
{
    public class Filler
    {
        private static async Task Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            string userId;
            string cpanelFilePath;
            string whmFilePath;
            string shellsFilePath;
            string cpanelsResetedFilePath;
            string smtpsFilePath;
            string loggedWordpressFilePath;

            if (arguments.Length < 8)
            {
                return;
            }

            userId = arguments[1];
            cpanelFilePath = arguments[2];
            whmFilePath = arguments[3];
            shellsFilePath = arguments[4];
            cpanelsResetedFilePath = arguments[5];
            smtpsFilePath = arguments[6];
            loggedWordpressFilePath = arguments[7];
            bool showOutput = arguments.Length > 8 && arguments[8] == "true";

            try
            {
                JsonAnswer answer = new();
                UserModel user = await UsersController.GetUserByIdAsync(long.Parse(userId));
                answer.CpanelAddedCount = await CheckFileAndPostValidAsync(user, cpanelFilePath, "cpanel");
                answer.WhmAddedCount = await CheckFileAndPostValidAsync(user, whmFilePath, "whm");
                answer.ShellsAddedCount = await CheckFileAndPostValidAsync(user, shellsFilePath, "shells");
                answer.CpanelsResetedAddedCount = await CheckFileAndPostValidAsync(user, cpanelsResetedFilePath, "cpanels-reseted");
                answer.SmtpsAddedCount = await CheckFileAndPostValidAsync(user, smtpsFilePath, "smtps");
                answer.LoggedWordpressAddedCount = await CheckFileAndPostValidAsync(user, loggedWordpressFilePath, "logged-wordpress");
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

        private static async Task<int> CheckFileAndPostValidAsync(UserModel user, string filePath, string category)
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
                ValidModel model = new()
                {
                    Data = row,
                    Category = category,
                    UploadedByUserId = user.Id,
                    UploadedByUsername = user.Username
                };
                if (await ValidController.PostValidAsync(model))
                {
                    addedCount++;
                }
            }
            return addedCount;
        }
    }

    public class JsonAnswer
    {
        public int CpanelAddedCount { get; set; }
        public int WhmAddedCount { get; set; }
        public int ShellsAddedCount { get; set; }
        public int CpanelsResetedAddedCount { get; set; }
        public int SmtpsAddedCount { get; set; }
        public int LoggedWordpressAddedCount { get; set; }
    }
}