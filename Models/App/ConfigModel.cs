using Prism.Mvvm;

namespace Models.App
{
    public class ConfigModel : BindableBase
    {
        private string botToken;
        private long telegramNotificationChat;
        private string currency;
        private string cpanelRegex;
        private string whmRegex;
        private string webmailRegex;
        private int pageMaxCount;
        private bool notifyWhenDatabaseFillNewLogRecords;

        public string BotToken { get => botToken; set => SetProperty(ref botToken, value); }
        public long TelegramNotificationChat { get => telegramNotificationChat; set => SetProperty(ref telegramNotificationChat, value); }
        public string Currency { get => currency; set => SetProperty(ref currency, value); }
        public string CpanelRegex { get => cpanelRegex; set => SetProperty(ref cpanelRegex, value); }
        public string WhmRegex { get => whmRegex; set => SetProperty(ref whmRegex, value); }
        public string WebmailRegex { get => webmailRegex; set => SetProperty(ref webmailRegex, value); }
        public int PageMaxCount { get => pageMaxCount; set => SetProperty(ref pageMaxCount, value); }
        public bool NotifyWhenDatabaseFillNewLogRecords { get => notifyWhenDatabaseFillNewLogRecords; set => SetProperty(ref notifyWhenDatabaseFillNewLogRecords, value); }
    }
}