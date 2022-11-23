using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
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
    public class PayoutsViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private readonly IEventAggregator aggregator;
        private UpdateHandler handler;

        private bool isLoading;
        private int modelsCount;
        private ObservableCollection<PayoutModel> models;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int ModelsCount { get => modelsCount; set => SetProperty(ref modelsCount, value); }
        public ObservableCollection<PayoutModel> Models { get => models; set => SetProperty(ref models, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public PayoutsViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            Models = new();
            Models.CollectionChanged += (s, e) => UpdateModelsCount();
            this.memory = memory;
            this.aggregator = aggregator;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<PayoutUpdateEvent>().Subscribe(OnPayoutUpdate);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
        }

        private async void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.Payouts)
            {
                await LoadPayoutsAsync();
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadPayoutsAsync());
        }

        private void InitModelCommands(PayoutModel model)
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
            model.MarkClosed = new DelegateCommand<PayoutModel>(OnMarkPayoutClosed);
        }

        private async void OnMarkPayoutClosed(PayoutModel model)
        {
            model.Status = "звершена";
            await PayoutController.PutPayoutAsync(model, aggregator);
            UserModel dbUser = await UsersController.GetUserByIdAsync(model.FromUserId);
            await handler.NotifyChangeStatusPayoutToClosed(dbUser);
        }

        private void UpdateModelsCount() => ModelsCount = Models.Count;

        private async Task LoadPayoutsAsync()
        {
            try
            {
                IsLoading = true;
                Models.Clear();
                List<PayoutModel> cacheModels = await PayoutController.GetPayoutsAsync();
                foreach (PayoutModel model in cacheModels)
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

        private void OnPayoutUpdate(KeyValuePair<string, PayoutModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                PayoutModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                Models.Add(updateModel);
                            }
                            break;

                        case "put":
                            {
                                PayoutModel updateModel = update.Value;
                                InitModelCommands(updateModel);
                                PayoutModel targetModel = Models.FirstOrDefault(u => u.Id == updateModel.Id);
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