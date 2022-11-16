using Prism.Mvvm;
using Unity;

namespace BotMainApp.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private string text;
        public string Text { get => text; set => SetProperty(ref text, value); }

        public SettingsViewModel(IUnityContainer container)
        {
            Text = "HUA";
        }
    }
}