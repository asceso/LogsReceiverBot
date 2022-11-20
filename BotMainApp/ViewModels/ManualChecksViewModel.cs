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
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BotMainApp.ViewModels
{
    public class ManualChecksViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private readonly IEventAggregator aggregator;
        private UpdateHandler handler;

        private bool isLoading;
        private int modelsCount;
        private ObservableCollection<ManualCheckModel> models;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public ObservableCollection<ManualCheckModel> Models { get => models; set => SetProperty(ref models, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public ManualChecksViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            Models = new();
            Models.CollectionChanged += (s, e) => UpdateModelsCount();
            this.memory = memory;
            this.aggregator = aggregator;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<ManualCheckUpdateEvent>().Subscribe(OnManualCheckUpdate);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.ManualChecks)
            {
                await LoadManualChecksAsync();
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadManualChecksAsync());
        }

        private void InitModelCommands(ManualCheckModel model)
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
            model.OpenManualCheckCommand = new DelegateCommand<ManualCheckModel>(OnOpenManualCheck);
        }

        private async void OnOpenManualCheck(ManualCheckModel model)
        {
            model.Status = CheckStatus.ManualCheckStatus.SendToManualChecking;
            await ManualCheckController.PutCheckAsync(model, aggregator);

            ManualCheckProcessWindow checkWindow = new(model);
            checkWindow.ShowDialog();
            if (checkWindow.DialogResult.HasValue && checkWindow.DialogResult.Value)
            {
            }
            else
            {
                model.Status = CheckStatus.ManualCheckStatus.CheckedBySoft;
                await ManualCheckController.PutCheckAsync(model, aggregator);
            }
        }

        private void UpdateModelsCount() => ModelsCount = Models.Count;

        private async Task LoadManualChecksAsync()
        {
            try
            {
                IsLoading = true;
                List<ManualCheckModel> cacheModels = await ManualCheckController.GetChecksAsync();
                foreach (ManualCheckModel model in cacheModels)
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

        private void OnManualCheckUpdate(KeyValuePair<string, ManualCheckModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                ManualCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                Models.Add(updateModel);
                            }
                            break;

                        case "put":
                            {
                                ManualCheckModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                ManualCheckModel targetModel = Models.FirstOrDefault(u => u.Id == updateModel.Id);
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