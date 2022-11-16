using BotMainApp.ViewModels;
using BotMainApp.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using Services.Implementation;
using Services.Interfaces;
using System.Windows;
using System.Windows.Threading;
using TelegramSimpleService;
using Unity;

namespace BotMainApp
{
    public partial class App : PrismApplication
    {
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<IJsonAdapter, JsonAdapter>();
            containerRegistry.Register<IMemorySaver, MemorySaver>();
            containerRegistry.Register<IKeyboardService, KeyboardService>();
        }

        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainView, MainViewModel>();
            ViewModelLocationProvider.Register<UsersView, UsersViewModel>();
        }

        protected override Window CreateShell() => Container.Resolve<MainView>();

        private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }
    }
}