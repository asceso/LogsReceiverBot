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
    public class WpLoginViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private readonly IEventAggregator aggregator;
        private UpdateHandler handler;

        private bool isLoading;
        private int modelsCount;
        private ObservableCollection<WpLoginCheckModel> models;
        private bool isClosedChecksShow;
        private bool isErrorChecksShow;
        private bool isOtherChecksShow;
        private bool isAfterCheckingDataShow;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public ObservableCollection<WpLoginCheckModel> Models { get => models; set => SetProperty(ref models, value); }
        public bool IsClosedChecksShow { get => isClosedChecksShow; set => SetProperty(ref isClosedChecksShow, value); }
        public bool IsErrorChecksShow { get => isErrorChecksShow; set => SetProperty(ref isErrorChecksShow, value); }
        public bool IsOtherChecksShow { get => isOtherChecksShow; set => SetProperty(ref isOtherChecksShow, value); }
        public bool IsAfterCheckingDataShow { get => isAfterCheckingDataShow; set => SetProperty(ref isAfterCheckingDataShow, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public WpLoginViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            Models = new();
            Models.CollectionChanged += (s, e) => UpdateModelsCount();
            IsClosedChecksShow = true;
            IsErrorChecksShow = true;
            IsOtherChecksShow = true;
            IsAfterCheckingDataShow = false;
            this.memory = memory;
            this.aggregator = aggregator;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<WpLoginCheckUpdateEvent>().Subscribe(OnManualCheckUpdate);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.WpLoginChecks)
            {
                await LoadManualChecksAsync();
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadManualChecksAsync());
        }

        private void InitModelCommands(WpLoginCheckModel model)
        {
            model.OnCopyCommand = new DelegateCommand<string>((field) =>
            {
                try
                {
                    switch (field)
                    {
                        case "UserId":
                            Clipboard.SetText(model.FromUserId.ToString());
                            break;

                        case "Username":
                            Clipboard.SetText(model.FromUsername.ToString());
                            break;
                    }
                }
                catch (System.Exception)
                {
                }
            });
            model.OpenManualCheckCommand = new DelegateCommand<WpLoginCheckModel>(OnOpenManualCheck);
            model.OpenOriginalFileCommand = new DelegateCommand<WpLoginCheckModel>(OnOpenOriginalFile);
            model.DeleteCheckCommand = new DelegateCommand<WpLoginCheckModel>(OnDeleteManualCheck);
        }

        private async void OnOpenManualCheck(WpLoginCheckModel model)
        {
            CheckStatus.ManualCheckStatus cancelStatus = model.Status;
            if (cancelStatus != CheckStatus.ManualCheckStatus.End && cancelStatus != CheckStatus.ManualCheckStatus.EndNoValid)
            {
                model.Status = CheckStatus.ManualCheckStatus.SendToManualChecking;
                await WpLoginCheckController.PutCheckAsync(model, aggregator);
            }
            WpLoginCheckProcessWindow checkWindow = new(model, config.NotepadPath, notificationManager);
            checkWindow.ShowDialog();
            if (checkWindow.DialogResult.HasValue && checkWindow.DialogResult.Value)
            {
                if (checkWindow.IsNoAnyValid)
                {
                    WpLoginCheckModel updateModel = checkWindow.CheckingModel;
                    UserModel checkUser = await UsersController.GetUserByIdAsync(updateModel.FromUserId);
                    updateModel.IsManualCheckEnd = true;
                    updateModel.Status = CheckStatus.ManualCheckStatus.EndNoValid;
                    await WpLoginCheckController.PutCheckAsync(updateModel, aggregator);

                    await handler.NotifyUserForEndCheckingFileNoValid(checkUser, updateModel.Id);
                }
                else
                {
                    if (checkWindow.TotalFoundedValid != 0 && checkWindow.AddBalance != 0)
                    {
                        WpLoginCheckModel updateModel = checkWindow.CheckingModel;

                        UserModel checkUser = await UsersController.GetUserByIdAsync(updateModel.FromUserId);
                        checkUser.LogsUploaded += checkWindow.TotalFoundedValid;
                        checkUser.Balance += checkWindow.AddBalance;
                        await UsersController.PutUserAsync(checkUser, aggregator);

                        updateModel.IsManualCheckEnd = true;
                        updateModel.Status = CheckStatus.ManualCheckStatus.End;
                        await WpLoginCheckController.PutCheckAsync(updateModel, aggregator);

                        await handler.NotifyUserForEndCheckingFile(checkUser, updateModel, checkWindow.TotalFoundedValid, checkWindow.AddBalance);
                    }
                    else
                    {
                        model.Status = cancelStatus;
                        await WpLoginCheckController.PutCheckAsync(model, aggregator);
                    }
                }
            }
            else
            {
                model.Status = cancelStatus;
                await WpLoginCheckController.PutCheckAsync(model, aggregator);
            }
        }

        private void OnDeleteManualCheck(WpLoginCheckModel model)
        {
            notificationManager.ShowButtonWindow("Удалить проверку?", "Подтверждение", async () =>
            {
                int deleteCount = await WpLoginCheckController.DeleteCheckAsync(model);
                if (deleteCount == 1)
                {
                    try
                    {
                        Directory.Delete(Environment.CurrentDirectory + "/checks/" + model.Id + "/", true);
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

        private void OnOpenOriginalFile(WpLoginCheckModel model)
        {
            if (!File.Exists(model.OriginalFilePath))
            {
                notificationManager.Show("Ошибка", "Не найден файл", type: NotificationType.Error);
            }
            else
            {
                Runner.RunTextFileInNotepad(config.NotepadPath, model.OriginalFilePath);
            }
        }

        private void UpdateModelsCount() => ModelsCount = Models.Count;

        private async Task LoadManualChecksAsync()
        {
            try
            {
                IsLoading = true;
                Models.Clear();
                List<WpLoginCheckModel> cacheModels = await WpLoginCheckController.GetChecksAsync();
                foreach (WpLoginCheckModel model in cacheModels)
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

        private void OnManualCheckUpdate(KeyValuePair<string, WpLoginCheckModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                WpLoginCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                Models.Add(updateModel);
                            }
                            break;

                        case "put":
                            {
                                WpLoginCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                WpLoginCheckModel targetModel = Models.FirstOrDefault(u => u.Id == updateModel.Id);
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