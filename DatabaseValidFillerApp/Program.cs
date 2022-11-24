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

            if (arguments.Length < 4)
            {
                return;
            }

            userId = arguments[1];
            cpanelFilePath = arguments[2];
            whmFilePath = arguments[3];
            bool showOutput = arguments.Length > 4 && arguments[4] == "true";

            try
            {
                JsonAnswer answer = new();
                UserModel user = await UsersController.GetUserByIdAsync(long.Parse(userId));
                answer.CpanelAddedCount = await CheckFileAndPostValidAsync(user, cpanelFilePath, "cpanel");
                answer.WhmAddedCount = await CheckFileAndPostValidAsync(user, whmFilePath, "whm");
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
    }
}