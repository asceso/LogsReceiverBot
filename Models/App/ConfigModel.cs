using Prism.Mvvm;

namespace Models.App
{
    public class ConfigModel : BindableBase
    {
        private string botToken;
        private long telegramNotificationChat;
        private string currency;
        private string urlRegex;
        private string loginRegex;
        private string passwordRegex;

        public string BotToken { get => botToken; set => SetProperty(ref botToken, value); }
        public long TelegramNotificationChat { get => telegramNotificationChat; set => SetProperty(ref telegramNotificationChat, value); }
        public string Currency { get => currency; set => SetProperty(ref currency, value); }
        public string UrlRegex { get => urlRegex; set => SetProperty(ref urlRegex, value); }
        public string LoginRegex { get => loginRegex; set => SetProperty(ref loginRegex, value); }
        public string PasswordRegex { get => passwordRegex; set => SetProperty(ref passwordRegex, value); }
    }
}