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
        private int checkerMaxForThread;
        private bool useOwnCpanelChecker;
        private long errorNotificationChat;
        private long notifyWhenDatabaseFillNewLogRecordsChat;
        private long notifyWhenCheckerEndWorkChat;

        public string BotToken { get => botToken; set => SetProperty(ref botToken, value); }
        public long TelegramNotificationChat { get => telegramNotificationChat; set => SetProperty(ref telegramNotificationChat, value); }
        public string Currency { get => currency; set => SetProperty(ref currency, value); }
        public string CpanelRegex { get => cpanelRegex; set => SetProperty(ref cpanelRegex, value); }
        public string WhmRegex { get => whmRegex; set => SetProperty(ref whmRegex, value); }
        public string WebmailRegex { get => webmailRegex; set => SetProperty(ref webmailRegex, value); }
        public int PageMaxCount { get => pageMaxCount; set => SetProperty(ref pageMaxCount, value); }
        public int CheckerMaxForThread { get => checkerMaxForThread; set => SetProperty(ref checkerMaxForThread, value); }
        public bool UseOwnCpanelChecker { get => useOwnCpanelChecker; set => SetProperty(ref useOwnCpanelChecker, value); }
        public long ErrorNotificationChat { get => errorNotificationChat; set => SetProperty(ref errorNotificationChat, value); }
        public long NotifyWhenDatabaseFillNewLogRecordsChat { get => notifyWhenDatabaseFillNewLogRecordsChat; set => SetProperty(ref notifyWhenDatabaseFillNewLogRecordsChat, value); }
        public long NotifyWhenCheckerEndWorkChat { get => notifyWhenCheckerEndWorkChat; set => SetProperty(ref notifyWhenCheckerEndWorkChat, value); }
    }
}