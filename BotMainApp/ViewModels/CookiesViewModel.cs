using BotMainApp.External;
using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
using BotMainApp.Views.Windows;
using DataAdapter.Controllers;
using DatabaseEvents;
using Models.App;
using Models.Database;
using Models.Enums;
using Notification.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BotMainApp.ViewModels
{
    public class CookiesViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private readonly IEventAggregator aggregator;
        private UpdateHandler handler;

        private bool isLoading;
        private int modelsCount;
        private ObservableCollection<CookieModel> models;
        private bool isClosedShow;
        private bool isOtherShow;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public ObservableCollection<CookieModel> Models { get => models; set => SetProperty(ref models, value); }
        public bool IsClosedShow { get => isClosedShow; set => SetProperty(ref isClosedShow, value); }
        public bool IsOtherShow { get => isOtherShow; set => SetProperty(ref isOtherShow, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public CookiesViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            Models = new();
            Models.CollectionChanged += (s, e) => UpdateModelsCount();
            IsClosedShow = true;
            IsOtherShow = true;
            this.memory = memory;
            this.aggregator = aggregator;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<CookieUpdateEvent>().Subscribe(OnCookieUpdate);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.Cookies)
            {
                await LoadCookiesAsync();
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadCookiesAsync());
        }

        private void InitModelCommands(CookieModel model)
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
                    }
                }
                catch (System.Exception)
                {
                }
            });
            model.OpenFolderCommand = new DelegateCommand<CookieModel>(OnOpenFolder);
            model.OpenUrlCommand = new DelegateCommand<CookieModel>(OnOpenUrl);
            model.DeleteCommand = new DelegateCommand<CookieModel>(OnDeleteCookieCheck);
            model.OpenCheckCommand = new DelegateCommand<CookieModel>(OnOpenCookiesCheck);
        }

        private async void OnOpenCookiesCheck(CookieModel model)
        {
            CheckStatus.CookieCheckStatus cancelStatus = model.Status;
            if (cancelStatus != CheckStatus.CookieCheckStatus.End && cancelStatus != CheckStatus.CookieCheckStatus.EndNoValid)
            {
                model.Status = CheckStatus.CookieCheckStatus.Proceed;
                await CookiesController.PutCookieAsync(model, aggregator);
            }
            CookiesProcessWindow checkWindow = new(model, notificationManager, config.Currency);
            checkWindow.ShowDialog();
            if (checkWindow.DialogResult.HasValue && checkWindow.DialogResult.Value)
            {
                CookieModel updateModel = checkWindow.CheckingModel;
                UserModel checkUser = await UsersController.GetUserByIdAsync(updateModel.UploadedByUserId);

                if (updateModel.Status == CheckStatus.CookieCheckStatus.EndNoValid)
                {
                    updateModel.Status = CheckStatus.CookieCheckStatus.EndNoValid;
                    updateModel.ValidFound = 0;
                    updateModel.BalanceToUser = 0;
                    await CookiesController.PutCookieAsync(updateModel, aggregator);
                    await handler.NotifyUserForEndCheckingCookiesNoValid(checkUser, updateModel.Id);
                }
                if (updateModel.Status == CheckStatus.CookieCheckStatus.End)
                {
                    updateModel.Status = CheckStatus.CookieCheckStatus.End;
                    await CookiesController.PutCookieAsync(updateModel, aggregator);
                    await handler.NotifyUserForEndCheckingCookies(checkUser, updateModel);
                }
            }
            else
            {
                model.Status = cancelStatus;
                await CookiesController.PutCookieAsync(model, aggregator);
            }
        }

        private void OnOpenFolder(CookieModel model)
        {
            string folderPath = model.FolderPath;
            if (Directory.Exists(folderPath))
            {
                Runner.RunExplorerWithPath(folderPath.Replace("/", "\\"));
            }
            else
            {
                notificationManager.Show("Ошибка", "Не найден путь к папке", type: NotificationType.Error);
            }
        }

        private void OnOpenUrl(CookieModel model)
        {
            if (!Runner.RunChromeWithLink(model.FileLink))
            {
                notificationManager.Show("Ошибка", "Не найден URL для открытия", type: NotificationType.Error);
            }
        }

        private void OnDeleteCookieCheck(CookieModel model)
        {
            notificationManager.ShowButtonWindow("Удалить проверку?", "Подтверждение", async () =>
            {
                int deleteCount = await CookiesController.DeleteCookieAsync(model);
                if (deleteCount == 1)
                {
                    try
                    {
                        Directory.Delete(model.FolderPath, true);
                    }
                    catch (Exception)
                    {
                    }
                    Models.Remove(model);
                    notificationManager.Show("Проверка удалена", NotificationType.Information);
                }
                ;
            });
        }

        private void UpdateModelsCount() => ModelsCount = Models.Count;

        private async Task LoadCookiesAsync()
        {
            try
            {
                IsLoading = true;
                Models.Clear();
                List<CookieModel> cacheModels = await CookiesController.GetCookiesAsync();
                foreach (CookieModel model in cacheModels)
                {
                    InitModelCommands(model);
                    Models.Add(model);
                }
                RaisePropertyChanged(nameof(Models));
            }
            catch (Exception)
            {
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnCookieUpdate(KeyValuePair<string, CookieModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                CookieModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                Models.Add(updateModel);
                            }
                            break;

                        case "put":
                            {
                                CookieModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                CookieModel targetModel = Models.FirstOrDefault(u => u.Id == updateModel.Id);
                                if (targetModel is not null)
                                {
                                    int index = Models.IndexOf(targetModel);
                                    Models.RemoveAt(index);
                                    Models.Insert(index, updateModel);
                                    break;
                                }
                            }
                            break;
                    }
                });
            }
            catch (Exception)
            {
            }
        }

        private void OnBotRestart() => handler = memory.GetItem<UpdateHandler>("Handler");
    }
}