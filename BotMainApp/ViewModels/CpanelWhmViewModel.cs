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
using Telegram.Bot.Types;

namespace BotMainApp.ViewModels
{
    public class CpanelWhmViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private readonly IEventAggregator aggregator;
        private UpdateHandler handler;

        private bool isLoading;
        private int modelsCount;
        private ObservableCollection<CpanelWhmCheckModel> models;
        private bool isClosedChecksShow;
        private bool isErrorChecksShow;
        private bool isOtherChecksShow;
        private bool isAfterCheckingDataShow;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public ObservableCollection<CpanelWhmCheckModel> Models { get => models; set => SetProperty(ref models, value); }
        public bool IsClosedChecksShow { get => isClosedChecksShow; set => SetProperty(ref isClosedChecksShow, value); }
        public bool IsErrorChecksShow { get => isErrorChecksShow; set => SetProperty(ref isErrorChecksShow, value); }
        public bool IsOtherChecksShow { get => isOtherChecksShow; set => SetProperty(ref isOtherChecksShow, value); }
        public bool IsAfterCheckingDataShow { get => isAfterCheckingDataShow; set => SetProperty(ref isAfterCheckingDataShow, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public CpanelWhmViewModel(IEventAggregator aggregator, IMemorySaver memory)
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

            aggregator.GetEvent<CpanelWhmCheckUpdateEvent>().Subscribe(OnManualCheckUpdate);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.CpanelWhmChecks)
            {
                await LoadManualChecksAsync();
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadManualChecksAsync());
        }

        private void InitModelCommands(CpanelWhmCheckModel model)
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
            model.OpenManualCheckCommand = new DelegateCommand<CpanelWhmCheckModel>(OnOpenManualCheck);
            model.OpenOriginalFileCommand = new DelegateCommand<CpanelWhmCheckModel>(OnOpenOriginalFile);
            model.DeleteCheckCommand = new DelegateCommand<CpanelWhmCheckModel>(OnDeleteManualCheck);
            model.ResendToSoftManualCommand = new DelegateCommand<CpanelWhmCheckModel>(OnResendToSoftCommand);
        }

        private async void OnOpenManualCheck(CpanelWhmCheckModel model)
        {
            CheckStatus.ManualCheckStatus cancelStatus = model.Status;
            if (cancelStatus != CheckStatus.ManualCheckStatus.End && cancelStatus != CheckStatus.ManualCheckStatus.EndNoValid)
            {
                model.Status = CheckStatus.ManualCheckStatus.SendToManualChecking;
                await CpanelWhmCheckController.PutCheckAsync(model, aggregator);
            }
            CpanelWhmCheckProcessWindow checkWindow = new(model, config.NotepadPath, notificationManager);
            checkWindow.ShowDialog();
            if (checkWindow.DialogResult.HasValue && checkWindow.DialogResult.Value)
            {
                if (checkWindow.IsNoAnyValid)
                {
                    CpanelWhmCheckModel updateModel = checkWindow.CheckingModel;
                    UserModel checkUser = await UsersController.GetUserByIdAsync(updateModel.FromUserId);
                    updateModel.IsManualCheckEnd = true;
                    updateModel.Status = CheckStatus.ManualCheckStatus.EndNoValid;
                    await CpanelWhmCheckController.PutCheckAsync(updateModel, aggregator);

                    await handler.NotifyUserForEndCheckingFileNoValid(checkUser, updateModel.Id);
                }
                else
                {
                    if (checkWindow.TotalFoundedValid != 0 && checkWindow.AddBalance != 0)
                    {
                        CpanelWhmCheckModel updateModel = checkWindow.CheckingModel;

                        UserModel checkUser = await UsersController.GetUserByIdAsync(updateModel.FromUserId);
                        checkUser.LogsUploaded += checkWindow.TotalFoundedValid;
                        checkUser.Balance += checkWindow.AddBalance;
                        await UsersController.PutUserAsync(checkUser, aggregator);

                        updateModel.IsManualCheckEnd = true;
                        updateModel.Status = CheckStatus.ManualCheckStatus.End;
                        await CpanelWhmCheckController.PutCheckAsync(updateModel, aggregator);

                        await handler.NotifyUserForEndCheckingFile(checkUser, updateModel, checkWindow.TotalFoundedValid, checkWindow.AddBalance);
                    }
                    else
                    {
                        model.Status = cancelStatus;
                        await CpanelWhmCheckController.PutCheckAsync(model, aggregator);
                    }
                }
            }
            else
            {
                model.Status = cancelStatus;
                await CpanelWhmCheckController.PutCheckAsync(model, aggregator);
            }
        }

        private void OnDeleteManualCheck(CpanelWhmCheckModel model)
        {
            notificationManager.ShowButtonWindow("Удалить проверку?", "Подтверждение", async () =>
            {
                int deleteCount = await CpanelWhmCheckController.DeleteCheckAsync(model);
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

        private void OnOpenOriginalFile(CpanelWhmCheckModel model)
        {
            if (!System.IO.File.Exists(model.OriginalFilePath))
            {
                notificationManager.Show("Ошибка", "Не найден файл", type: NotificationType.Error);
            }
            else
            {
                Runner.RunTextFileInNotepad(config.NotepadPath, model.OriginalFilePath);
            }
        }

        private async void OnResendToSoftCommand(CpanelWhmCheckModel model)
        {
            UserModel modelUser = await UsersController.GetUserByIdAsync(model.FromUserId);
            await Task.Factory
                .StartNew(async () => await handler.StandartCheckProcessForPort20(modelUser, model.OriginalFilePath, model, !model.IsDublicatesFilledToDb, !model.IsDublicatesFilledToDb, true))
                .ConfigureAwait(false);
        }

        private void UpdateModelsCount() => ModelsCount = Models.Count;

        private async Task LoadManualChecksAsync()
        {
            try
            {
                IsLoading = true;
                Models.Clear();
                List<CpanelWhmCheckModel> cacheModels = await CpanelWhmCheckController.GetChecksAsync();
                foreach (CpanelWhmCheckModel model in cacheModels)
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

        private void OnManualCheckUpdate(KeyValuePair<string, CpanelWhmCheckModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                CpanelWhmCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                Models.Add(updateModel);
                            }
                            break;

                        case "put":
                            {
                                CpanelWhmCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                CpanelWhmCheckModel targetModel = Models.FirstOrDefault(u => u.Id == updateModel.Id);
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