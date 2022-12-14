using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
using DataAdapter.Controllers;
using DatabaseEvents;
using Microsoft.Win32;
using Models.App;
using Models.Database;
using Models.Enums;
using Notification.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BotMainApp.ViewModels
{
    public class ValidViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private UpdateHandler handler;
        private int currentPage;
        private bool isSwitchingPage;

        private bool isLoading;
        private int modelsCount;
        private int maxPageCount;
        private ObservableCollection<ValidModel> allData;
        private ObservableCollection<UserModel> usersForFilter;
        private ObservableCollection<string> categoriesForFilter;
        private UserModel selectedUserForFilter;
        private string selectedCategoryForFilter;
        private CollectionViewSource modelsForView;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public int MaxPageCount { get => maxPageCount; set => SetProperty(ref maxPageCount, value); }
        public ObservableCollection<ValidModel> AllData { get => allData; set => SetProperty(ref allData, value); }
        public ObservableCollection<UserModel> UsersForFilter { get => usersForFilter; set => SetProperty(ref usersForFilter, value); }
        public ObservableCollection<string> CategoriesForFilter { get => categoriesForFilter; set => SetProperty(ref categoriesForFilter, value); }
        public CollectionViewSource ModelsForView { get => modelsForView; set => SetProperty(ref modelsForView, value); }

        public UserModel SelectedUserForFilter
        {
            get => selectedUserForFilter ?? new();
            set
            {
                bool switchToNull = selectedUserForFilter != null && value == null;
                SetProperty(ref selectedUserForFilter, value);
                if (!isSwitchingPage && !switchToNull)
                {
                    Task.Run(async () => await ReloadByPage(currentPage, true));
                }
            }
        }

        public string SelectedCategoryForFilter
        {
            get => selectedCategoryForFilter ?? string.Empty;
            set
            {
                bool switchToNull = selectedCategoryForFilter != null && value == null;
                SetProperty(ref selectedCategoryForFilter, value);
                if (!isSwitchingPage && !switchToNull)
                {
                    Task.Run(async () => await ReloadByPage(currentPage, true));
                }
            }
        }

        public DelegateCommand RefreshCommand { get; set; }
        public DelegateCommand SaveToFileCurrentViewLogs { get; set; }

        public ValidViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            AllData = new();
            ModelsForView = new();
            UsersForFilter = new();
            CategoriesForFilter = new();
            this.memory = memory;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<DublicateUpdateEvent>().Subscribe(OnLogUpdates);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private void OnLogUpdates()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    await LoadValidsAsync();
                    await ReloadByPage(currentPage);
                });
            }
            catch (Exception)
            {
            }
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.Valid)
            {
                isSwitchingPage = true;
                await Task.Run(async () =>
                {
                    await InitComboItemsAsync();
                    await LoadValidsAsync();
                    await ReloadByPage(1);
                });
                isSwitchingPage = false;
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () =>
            {
                await LoadValidsAsync();
                await ReloadByPage(currentPage);
            });
            SaveToFileCurrentViewLogs = new DelegateCommand(OnSaveToFileCurrentLogs);
        }

        private void InitModelCommands(ValidModel model)
        {
            model.OnCopyCommand = new DelegateCommand<string>((field) =>
            {
                try
                {
                    switch (field)
                    {
                        case "UserId":
                            Clipboard.SetText(model.UploadedByUserId.ToString());
                            break;

                        case "Username":
                            Clipboard.SetText(model.UploadedByUsername.ToString());
                            break;

                        case "LogData":
                            Clipboard.SetText(model.Data);
                            break;
                    }
                }
                catch (System.Exception)
                {
                }
            });
        }

        public async Task ReloadByPage(int toPage, bool updateLogs = false)
        {
            try
            {
                IsLoading = true;
                if (updateLogs)
                {
                    await LoadValidsAsync();
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ModelsForView.Source = AllData.Skip(config.PageMaxCount * (toPage - 1)).Take(config.PageMaxCount * toPage);
                    MaxPageCount = AllData.Count / config.PageMaxCount;
                    ModelsForView.View?.Refresh();
                    ModelsCount = AllData.Count;
                });
                currentPage = toPage;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadValidsAsync()
        {
            try
            {
                IsLoading = true;
                AllData.Clear();
                AllData.AddRange(await ValidController.GetValidAsync(SelectedUserForFilter, SelectedCategoryForFilter));
                foreach (ValidModel model in AllData)
                {
                    InitModelCommands(model);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InitComboItemsAsync()
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                SelectedUserForFilter = null;
                UsersForFilter.Clear();
                UsersForFilter.AddRange(await UsersController.GetUsersAsync());
                RaisePropertyChanged(nameof(UsersForFilter));

                SelectedCategoryForFilter = null;
                CategoriesForFilter.Clear();
                CategoriesForFilter.AddRange(ValidController.GetValidCategories());
                RaisePropertyChanged(nameof(CategoriesForFilter));
            });
        }

        private async void OnSaveToFileCurrentLogs()
        {
            SaveFileDialog sfd = new()
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filter = "Текстовый файл | *.txt"
            };
            bool? saveFileDialog = sfd.ShowDialog();
            if (saveFileDialog.HasValue && saveFileDialog.Value)
            {
                try
                {
                    IsLoading = true;
                    using TextWriter writer = new StreamWriter(sfd.FileName);
                    foreach (ValidModel model in AllData)
                    {
                        await writer.WriteLineAsync(model.Data);
                    }
                    writer.Close();
                    notificationManager.Show("Результат", $"Файл успешно сохранен", NotificationType.Information);
                }
                catch (Exception)
                {
                    notificationManager.Show("Результат", $"Ошибка при выгрузке файла", NotificationType.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private void OnBotRestart() => handler = memory.GetItem<UpdateHandler>("Handler");
    }
}