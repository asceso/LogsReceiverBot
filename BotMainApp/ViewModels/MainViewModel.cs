using BotMainApp.Events;
using BotMainApp.Telegram;
using Models.Enums;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Services;
using Services.Interfaces;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Telegram.Bot;
using TelegramSimpleService;

namespace BotMainApp.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region fields

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

            memory.StoreItem("Locales", locales);
            memory.StoreItem("Keyboards", keyboards);
            memory.StoreItem("Operations", operations);

            TelegramBotClient botClient = new(config.BotToken);
            memory.StoreItem("BotClient", botClient);

            UpdateHandler handler = new(aggregator, memory);
            botClient.StartReceiving(handler);
            memory.StoreItem("Handler", handler);

            if (!Directory.Exists(PathCollection.TempFolderPath)) Directory.CreateDirectory(PathCollection.TempFolderPath);
            TelegramState.SetInfo("работает");
            aggregator.GetEvent<TelegramStateEvent>().Subscribe((st) =>
            {
                TelegramState.Set(st.Status, st.Color);
                RaisePropertyChanged(nameof(TelegramState));
            });
            InitCommands();
        }

        #endregion ctor

        #region cmd

        public DelegateCommand OpenMainWindowCommand { get; set; }
        public DelegateCommand CloseAppCommand { get; set; }
        public DelegateCommand<string> SwithViewCommand { get; set; }
        public DelegateCommand TestCommand { get; set; }

        #endregion cmd

        #region cmd executors

        private void InitCommands()
        {
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

        private void OnTest()
        {
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