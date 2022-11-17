using BotMainApp.Events;
using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
using DataAdapter.Controllers;
using Microsoft.Win32;
using Models.App;
using Models.Database;
using Models.Enums;
using Notification.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Services;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Telegram.Bot;
using TelegramSimpleService;

namespace BotMainApp.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region fields

        private NotificationManager notificationManager;
        private CancellationTokenSource cancelationTokenSource;

        private TelegramStateModel telegramState;
        private ViewsPayload.ViewTypes currentView;
        private string title;
        private Visibility trayIconVisibility;
        private WindowState currentWindowState;
        private bool showInTaskbar;

        #endregion fields

        #region props

        public TelegramStateModel TelegramState { get => telegramState; set => SetProperty(ref telegramState, value); }
        public ViewsPayload.ViewTypes CurrentView { get => currentView; set => SetProperty(ref currentView, value); }
        public string Title { get => title; set => SetProperty(ref title, value); }
        public Visibility TrayIconVisibility { get => trayIconVisibility; set => SetProperty(ref trayIconVisibility, value); }
        public WindowState CurrentWindowState { get => currentWindowState; set => SetProperty(ref currentWindowState, value); }
        public bool ShowInTaskbar { get => showInTaskbar; set => SetProperty(ref showInTaskbar, value); }

        #endregion props

        #region ctor

        public MainViewModel(IJsonAdapter jsonAdapter,
                             IMemorySaver memory,
                             IKeyboardService keyboardService,
                             IEventAggregator aggregator)
        {
            Title = "Бот для приема логов";
            TelegramState = new("запуск", TelegramStateModel.BlackBrush);
            TrayIconVisibility = Visibility.Collapsed;
            ShowInTaskbar = true;
            CurrentWindowState = WindowState.Maximized;
            CurrentView = ViewsPayload.ViewTypes.Users;

            keyboardService.SetStoreFileName("/config/keys.json", null);
            var config = jsonAdapter.ReadJsonConfig();
            var operations = jsonAdapter.ReadJsonOperations();
            var locales = jsonAdapter.ReadJsonLocaleStrings();
            var keyboards = keyboardService.LoadOneRowKeyboards();

            notificationManager = new();
            memory.StoreItem("Notification", notificationManager);

            memory.StoreItem("Config", config);
            memory.StoreItem("Locales", locales);
            memory.StoreItem("Keyboards", keyboards);
            memory.StoreItem("Operations", operations);

            InitCommands(config, aggregator, memory);
            CreateAndStartBot.Execute();

            if (!Directory.Exists(PathCollection.TempFolderPath)) Directory.CreateDirectory(PathCollection.TempFolderPath);
            TelegramState.SetInfo("работает");
            aggregator.GetEvent<TelegramStateEvent>().Subscribe((st) =>
            {
                TelegramState.Set(st.Status, st.Color);
                RaisePropertyChanged(nameof(TelegramState));
            });
        }

        #endregion ctor

        #region cmd

        public DelegateCommand OpenMainWindowCommand { get; set; }
        public DelegateCommand CloseAppCommand { get; set; }
        public DelegateCommand<string> SwithViewCommand { get; set; }
        public DelegateCommand CreateAndStartBot { get; set; }
        public DelegateCommand TestCommand { get; set; }

        #endregion cmd

        #region cmd executors

        private void InitCommands(ConfigModel config, IEventAggregator aggregator, IMemorySaver memory)
        {
            CreateAndStartBot = new DelegateCommand(() => OnCreateAndStartBot(config, aggregator, memory));
            OpenMainWindowCommand = new DelegateCommand(OnOpenMainWindow);
            CloseAppCommand = new DelegateCommand(OnCloseApp);
            SwithViewCommand = new DelegateCommand<string>(OnSwitchView);
            TestCommand = new DelegateCommand(OnTest);
        }

        private void OnOpenMainWindow()
        {
            TrayIconVisibility = Visibility.Hidden;
            ShowInTaskbar = true;
            CurrentWindowState = WindowState.Maximized;
        }

        private void OnCloseApp()
        {
            TrayIconVisibility = Visibility.Collapsed;
            App.Current.Shutdown();
        }

        private void OnSwitchView(string obj) => CurrentView = ViewsPayload.GetByName(obj.ToString());

        private async void OnCreateAndStartBot(ConfigModel config, IEventAggregator aggregator, IMemorySaver memory)
        {
            if (cancelationTokenSource != null) cancelationTokenSource.Cancel();
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("перезапуск", TelegramStateModel.BlackBrush));
            await Task.Delay(TimeSpan.FromSeconds(5));

            TelegramBotClient botClient = new(config.BotToken);
            if (memory.ItemExist("BotClient")) memory.RemoveItem("BotClient");
            memory.StoreItem("BotClient", botClient);

            UpdateHandler handler = new(aggregator, memory);
            cancelationTokenSource = new();
            botClient.StartReceiving(handler, cancellationToken: cancelationTokenSource.Token);
            if (memory.ItemExist("Handler")) memory.RemoveItem("Handler");
            memory.StoreItem("Handler", handler);

            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
            aggregator.GetEvent<BotRestartEvent>().Publish();
        }

        private void OnTest()
        {
            int take = 10000;
            int rewrite = 2000;
            List<LogModel> logs = LogsController.TakeShuffleLogs(take);
            string filename = "C:\\Users\\Uchidero\\Documents\\!Files\\" + take + "logs" + rewrite + "random.txt";
            using StreamWriter writer = new(filename);
            int randomCount = 0;
            foreach (var log in logs)
            {
                if (Random.Shared.NextDouble() > 0.65 && randomCount < 300)
                {
                    writer.WriteLine(log.ToString().Remove(log.ToString().Length - 4) + Random.Shared.Next(1000, 9999));
                    randomCount++;
                }
                else
                {
                    writer.WriteLine(log.ToString());
                }
            }
            writer.Close();
            notificationManager.Show("Выполнено", "Файл сохранен.", type: NotificationType.Warning);
        }

        #endregion cmd executors
    }

    public class TelegramStateModel : BindableBase
    {
        private string status;
        private SolidColorBrush color;

        public string Status { get => status; set => SetProperty(ref status, value); }
        public SolidColorBrush Color { get => color; set => SetProperty(ref color, value); }

        public void SetDefault(string status) => Set(status, BlackBrush);

        public void SetInfo(string status) => Set(status, GreenBrush);

        public void SetError(string status) => Set(status, RedBrush);

        public TelegramStateModel(string status, SolidColorBrush color)
        {
            Status = status;
            Color = color;
        }

        public void Set(string status, SolidColorBrush color)
        {
            Status = status;
            Color = color;
        }

        public static SolidColorBrush BlackBrush = new(Colors.Black);
        public static SolidColorBrush RedBrush = new(Colors.Red);
        public static SolidColorBrush GreenBrush = new(Colors.Green);
    }
}