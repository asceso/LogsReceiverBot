using BotMainApp.ViewModels;
using BotMainApp.Views;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using Services.Implementation;
using Services.Interfaces;
using System;
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
            ViewModelLocationProvider.Register<LogsView, LogsViewModel>();
            ViewModelLocationProvider.Register<ManualChecksView, ManualChecksViewModel>();
        }

        protected override Window CreateShell()
        {
            App.Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;
            return Container.Resolve<MainView>();
        }

        private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var manager = new Notification.Wpf.NotificationManager();
            manager.Show(e.Exception, expirationTime: TimeSpan.FromMinutes(10));
            e.Handled = true;
        }
    }
}