using Prism.Mvvm;

namespace Models.App
{
    public class ConfigModel : BindableBase
    {
        private string botToken;
        private int requestsPerMinuteAutoBan;
        private int captchaTimer;
        private int captchaAttemptNum;
        private int minBalanceForPayment;
        private string currency;
        private List<string> payoutMethods;
        private string cpanelRegex;
        private string whmRegex;
        private string webmailRegex;
        private int pageMaxCount;
        private int checkerMaxForThread;
        private bool useOwnCpanelChecker;
        private Chats chats;

        public string BotToken { get => botToken; set => SetProperty(ref botToken, value); }
        public int RequestsPerMinuteAutoBan { get => requestsPerMinuteAutoBan; set => SetProperty(ref requestsPerMinuteAutoBan, value); }
        public int CaptchaTimer { get => captchaTimer; set => SetProperty(ref captchaTimer, value); }
        public int CaptchaAttemptNum { get => captchaAttemptNum; set => SetProperty(ref captchaAttemptNum, value); }
        public int MinBalanceForPayment { get => minBalanceForPayment; set => SetProperty(ref minBalanceForPayment, value); }
        public string Currency { get => currency; set => SetProperty(ref currency, value); }
        public List<string> PayoutMethods { get => payoutMethods; set => SetProperty(ref payoutMethods, value); }
        public string CpanelRegex { get => cpanelRegex; set => SetProperty(ref cpanelRegex, value); }
        public string WhmRegex { get => whmRegex; set => SetProperty(ref whmRegex, value); }
        public string WebmailRegex { get => webmailRegex; set => SetProperty(ref webmailRegex, value); }
        public int PageMaxCount { get => pageMaxCount; set => SetProperty(ref pageMaxCount, value); }
        public int CheckerMaxForThread { get => checkerMaxForThread; set => SetProperty(ref checkerMaxForThread, value); }
        public bool UseOwnCpanelChecker { get => useOwnCpanelChecker; set => SetProperty(ref useOwnCpanelChecker, value); }
        public Chats Chats { get => chats; set => SetProperty(ref chats, value); }
    }

    public class Chats : BindableBase
    {
        private long errorNotificationChat;
        private long notifyWhenDatabaseFillNewLogRecordsChat;
        private long notifyWhenDatabaseFillNewValidRecordsChat;
        private long notifyWhenCheckerEndWorkChat;
        private long notifyWhenUserMakePayout;

        public long ErrorNotificationChat { get => errorNotificationChat; set => SetProperty(ref errorNotificationChat, value); }
        public long NotifyWhenDatabaseFillNewLogRecordsChat { get => notifyWhenDatabaseFillNewLogRecordsChat; set => SetProperty(ref notifyWhenDatabaseFillNewLogRecordsChat, value); }
        public long NotifyWhenDatabaseFillNewValidRecordsChat { get => notifyWhenDatabaseFillNewValidRecordsChat; set => SetProperty(ref notifyWhenDatabaseFillNewValidRecordsChat, value); }
        public long NotifyWhenCheckerEndWorkChat { get => notifyWhenCheckerEndWorkChat; set => SetProperty(ref notifyWhenCheckerEndWorkChat, value); }
        public long NotifyWhenUserMakePayout { get => notifyWhenUserMakePayout; set => SetProperty(ref notifyWhenUserMakePayout, value); }
    }
}