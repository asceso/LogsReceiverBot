using BotMainApp.ViewModels;
using BotMainApp.Views;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using Services;
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
            containerRegistry.Register<ICaptchaService, CaptchaService>();
            containerRegistry.Register<IKeyboardService, KeyboardService>();
            containerRegistry.Register<ITaskScheduleService, TaskScheduleService>();
        }

        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();
            ViewModelLocationProvider.Register<MainView, MainViewModel>();
            ViewModelLocationProvider.Register<UsersView, UsersViewModel>();
            ViewModelLocationProvider.Register<DublicatesView, DublicatesViewModel>();
            ViewModelLocationProvider.Register<ValidView, ValidViewModel>();
            ViewModelLocationProvider.Register<CookiesView, CookiesViewModel>();
            ViewModelLocationProvider.Register<CpanelWhmView, CpanelWhmViewModel>();
            ViewModelLocationProvider.Register<WpLoginView, WpLoginViewModel>();
            ViewModelLocationProvider.Register<PayoutsView, PayoutsViewModel>();
        }

        protected override Window CreateShell()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1] == "useOwnConfig=true")
            {
                PathCollection.ConfigPath = PathCollection.ConfigPath.Replace("config.json", "config_my.json");
                var manager = new Notification.Wpf.NotificationManager();
                manager.Show("используется конфиг config_my.json");
                manager = null;
            }
            Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;
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