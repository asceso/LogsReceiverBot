using Prism.Mvvm;

namespace Models.App
{
    public class ConfigModel : BindableBase
    {
        private string botToken;

        public string BotToken { get => botToken; set => SetProperty(ref botToken, value); }
    }
}