using BotMainApp.Events;
using BotMainApp.Telegram;
using Microsoft.Win32;
using Models.App;
using Models.Enums;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Services;
using Services.Interfaces;
using SimpleLogger.FileService;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Telegram.Bot;
using TelegramSimpleService;
using Unity;

namespace BotMainApp.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region services

        private readonly IJsonAdapter jsonAdapter;
        private readonly IUniqueCreator uniqueCreator;
        private readonly IFileLogger logger;
        private readonly IKeyboardService keyboardService;
        private readonly IEventAggregator aggregator;

        #endregion services

        #region other vm

        private SettingsViewModel settingsVM;
        private UsersViewModel usersVM;

        public SettingsViewModel SettingsVM { get => settingsVM; set => SetProperty(ref settingsVM, value); }
        public UsersViewModel UsersVM { get => usersVM; set => SetProperty(ref usersVM, value); }

        #endregion other vm

        #region fields

        private TelegramBotClient botClient;
        private ConfigModel config;
        private TelegramStateModel telegramState;
        private ViewsPayload.ViewTypes currentView;

        private string title;
        private Visibility trayIconVisibility;
        private WindowState currentWindowState;
        private bool showInTaskbar;

        #endregion fields

        #region props

        public TelegramBotClient BotClient { get => botClient; set => SetProperty(ref botClient, value); }
        public ConfigModel Config { get => config; set => SetProperty(ref config, value); }
        public TelegramStateModel TelegramState { get => telegramState; set => SetProperty(ref telegramState, value); }
        public ViewsPayload.ViewTypes CurrentView { get => currentView; set => SetProperty(ref currentView, value); }

        public string Title { get => title; set => SetProperty(ref title, value); }
        public Visibility TrayIconVisibility { get => trayIconVisibility; set => SetProperty(ref trayIconVisibility, value); }
        public WindowState CurrentWindowState { get => currentWindowState; set => SetProperty(ref currentWindowState, value); }
        public bool ShowInTaskbar { get => showInTaskbar; set => SetProperty(ref showInTaskbar, value); }

        #endregion props

        #region ctor

        public MainViewModel(IUniqueCreator uniqueCreator, IJsonAdapter jsonAdapter, IFileLogger logger, IKeyboardService keyboardService, IEventAggregator aggregator, IUnityContainer container)
        {
            Title = "Бот для приема логов";
            TelegramState = new("запуск", TelegramStateModel.BlackBrush);

            this.uniqueCreator = uniqueCreator;
            this.jsonAdapter = jsonAdapter;
            this.logger = logger;
            this.keyboardService = keyboardService;
            this.aggregator = aggregator;

            TrayIconVisibility = Visibility.Collapsed;
            ShowInTaskbar = true;
            CurrentWindowState = WindowState.Maximized;
            //CurrentView = ViewsPayload.ViewTypes.Settings;
            CurrentView = ViewsPayload.ViewTypes.Users;

            SettingsVM = container.Resolve<SettingsViewModel>();
            UsersVM = container.Resolve<UsersViewModel>();
            InitCommands();
            InitAsync();
        }

        private async void InitAsync()
        {
            try
            {
                PathCollection.SetExecutablePath();
                if (!Directory.Exists(PathCollection.TempFolderPath)) Directory.CreateDirectory(PathCollection.TempFolderPath);
                Config = await jsonAdapter.ReadJsonConfigAsync();
                logger.InitLogsFolder();

                BotClient = new(Config.BotToken);
                UpdateHandler handler = new();
                await handler.ConfigureServicesAsync(logger, jsonAdapter, keyboardService, aggregator, BotClient);
                BotClient.StartReceiving(handler);

                TelegramState.SetInfo("работает");
                aggregator.GetEvent<TelegramStateEvent>().Subscribe((st) =>
                {
                    TelegramState.Set(st.Status, st.Color);
                    RaisePropertyChanged(nameof(TelegramState));
                });
                logger.Info("shell init done");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }
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

        private async void OnTest()
        {
            string testFilePath = Environment.CurrentDirectory + "/TESTFILE.txt";
            string tempfolder = PathCollection.TempFolderPath + uniqueCreator.GetCurentDateTimeString() + "/";

            Directory.CreateDirectory(tempfolder);
            foreach (string file in Directory.GetFiles(PathCollection.CheckerBinPath))
            {
                FileInfo fi = new(file);
                File.Copy(file, tempfolder + fi.Name, true);
            }
            File.Copy(testFilePath, tempfolder + "/input.txt");
            testFilePath = tempfolder + "/input.txt";

            ProcessStartInfo psi = new()
            {
                FileName = tempfolder + "checkcp.exe",
                WorkingDirectory = tempfolder,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process process = new()
            {
                StartInfo = psi
            };
            process.Start();

            using StreamWriter writer = process.StandardInput;
            await Task.Delay(TimeSpan.FromSeconds(1));
            await writer.WriteLineAsync("1");
            await writer.WriteLineAsync("input.txt");
            writer.Close();

            await process.WaitForExitAsync();
            process.Close();
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