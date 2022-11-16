using BotMainApp.ViewModels;
using BotMainApp.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using Services.Implementation;
using Services.Interfaces;
using SimpleLogger.FileService;
using System.Windows;
using TelegramSimpleService;

namespace BotMainApp
{
    public partial class App : PrismApplication
    {
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<IJsonAdapter, JsonAdapter>();
            containerRegistry.Register<IUniqueCreator, UniqueCreator>();
            containerRegistry.Register<IFileLogger, FileLogger>();
            containerRegistry.Register<IKeyboardService, KeyboardService>();
            containerRegistry.Register<IEventAggregator, EventAggregator>();

            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<UsersView, UsersViewModel>();
        }

        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainView>(() => Container.Resolve<MainViewModel>());
            ViewModelLocationProvider.Register<SettingsView>(() => Container.Resolve<SettingsViewModel>());
        }

        protected override Window CreateShell() => Container.Resolve<MainView>();
    }
}