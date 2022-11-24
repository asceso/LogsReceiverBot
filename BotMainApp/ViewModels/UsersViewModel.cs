using AgregatorEvents;
using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
using BotMainApp.Views.Windows;
using DataAdapter.Controllers;
using Extensions;
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
    public class UsersViewModel : BindableBase
    {
        private readonly bool isDebug = false;
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private UpdateHandler handler;

        private bool isLoading;
        private int notAcceptedUsersCount;
        private int dataUsersCount;
        private ObservableCollection<UserModel> notAcceptedUsers;
        private ObservableCollection<UserModel> dataUsers;
        private bool isHasSelectedDataUsers;
        private bool isBannedUsersShow;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int NotAcceptedUsersCount { get => notAcceptedUsersCount; set => SetProperty(ref notAcceptedUsersCount, value); }
        public int DataUsersCount { get => dataUsersCount; set => SetProperty(ref dataUsersCount, value); }
        public ObservableCollection<UserModel> NotAcceptedUsers { get => notAcceptedUsers; set => SetProperty(ref notAcceptedUsers, value); }
        public ObservableCollection<UserModel> DataUsers { get => dataUsers; set => SetProperty(ref dataUsers, value); }
        public bool IsHasSelectedDataUsers { get => isHasSelectedDataUsers; set => SetProperty(ref isHasSelectedDataUsers, value); }
        public bool IsBannedUsersShow { get => isBannedUsersShow; set => SetProperty(ref isBannedUsersShow, value); }

        public DelegateCommand RefreshCommand { get; set; }
        public DelegateCommand SelectAllUsersCommand { get; set; }
        public DelegateCommand UnselectAllUsersCommand { get; set; }
        public DelegateCommand BlockSelectedUsersCommand { get; set; }
        public DelegateCommand UnblockSelectedUsersCommand { get; set; }
        public DelegateCommand SendMailToSelectedUsersCommand { get; set; }

        public UsersViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            NotAcceptedUsers = new();
            DataUsers = new();
            IsBannedUsersShow = true;
            NotAcceptedUsers.CollectionChanged += (s, e) => UpdateNotAcceptedUsersCounter();
            DataUsers.CollectionChanged += (s, e) => UpdateDataUsersCounter();

            this.memory = memory;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");

            aggregator.GetEvent<UserUpdateEvent>().Subscribe(OnUserUpdates);
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
            aggregator.GetEvent<SwitchViewTypeEvent>().Subscribe(OnSwitchMainView);

            InitVmCommands();
            RefreshCommand.Execute();
        }

        private void OnSwitchMainView(ViewsPayload.ViewTypes selectedType)
        {
            if (selectedType is ViewsPayload.ViewTypes.Users)
            {
                RefreshCommand.Execute();
            }
        }

        private void OnUserUpdates(KeyValuePair<string, UserModel> update)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (update.Key)
                    {
                        case "post":
                            {
                                UserModel updateUser = update.Value;
                                InitUserCommands(updateUser);
                                NotAcceptedUsers.Add(updateUser);
                            }
                            break;

                        case "put":
                            {
                                UserModel updateUser = update.Value;
                                InitUserCommands(updateUser);
                                UserModel targetUser = NotAcceptedUsers.FirstOrDefault(u => u.Id == updateUser.Id);
                                if (targetUser is not null)
                                {
                                    int index = NotAcceptedUsers.IndexOf(targetUser);
                                    NotAcceptedUsers.RemoveAt(index);
                                    NotAcceptedUsers.Insert(index, updateUser);
                                    break;
                                }
                                targetUser = DataUsers.FirstOrDefault(u => u.Id == updateUser.Id);
                                if (targetUser is not null)
                                {
                                    int index = DataUsers.IndexOf(targetUser);
                                    DataUsers.RemoveAt(index);
                                    DataUsers.Insert(index, updateUser);
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

        private async Task LoadUsersAsync()
        {
            try
            {
                IsLoading = true;
                NotAcceptedUsers.Clear();
                DataUsers.Clear();
                List<UserModel> cacheUsers = new();
                if (isDebug)
                {
                    for (int i = 0; i < 100; i++)
                    {
                        UserModel user = new();
                        user.FillRandom();
                        cacheUsers.Add(user);
                    }
                }
                else
                {
                    cacheUsers = await UsersController.GetUsersAsync();
                }
                foreach (UserModel user in cacheUsers.Where(u => !u.IsAccepted)) NotAcceptedUsers.Add(user);
                foreach (UserModel user in cacheUsers.Where(u => u.IsAccepted).OrderBy(u => u.Username)) DataUsers.Add(user);
                foreach (UserModel user in NotAcceptedUsers) InitUserCommands(user);
                foreach (UserModel user in DataUsers) InitUserCommands(user);
                RaisePropertyChanged(nameof(NotAcceptedUsers));
                RaisePropertyChanged(nameof(DataUsers));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InitVmCommands()
        {
            RefreshCommand = new DelegateCommand(async () => await LoadUsersAsync());
            SelectAllUsersCommand = new DelegateCommand(() =>
            {
                foreach (UserModel user in DataUsers)
                {
                    user.IsSelected = true;
                }
            });
            UnselectAllUsersCommand = new DelegateCommand(() =>
            {
                foreach (UserModel user in DataUsers)
                {
                    user.IsSelected = false;
                }
            });
            BlockSelectedUsersCommand = new DelegateCommand(async () =>
            {
                int successCount = await handler.MoveUsersToBL(DataUsers.Where(u => u.IsSelected).ToList());
                notificationManager.Show("Результат", $"Заблокировано {successCount}/{DataUsers.Count(u => u.IsSelected)} пользователей", NotificationType.Information);
            }, () => IsHasSelectedDataUsers).ObservesProperty(() => IsHasSelectedDataUsers);
            UnblockSelectedUsersCommand = new DelegateCommand(async () =>
            {
                int successCount = await handler.MoveUsersFromBL(DataUsers.Where(u => u.IsSelected).ToList());
                notificationManager.Show("Результат", $"Разблокировано {successCount}/{DataUsers.Count(u => u.IsSelected)} пользователей", NotificationType.Information);
            }, () => IsHasSelectedDataUsers).ObservesProperty(() => IsHasSelectedDataUsers);
            SendMailToSelectedUsersCommand = new DelegateCommand(async () =>
            {
                using SendMailWindow smw = new($"{DataUsers.Count(u => u.IsSelected)} выбранных пользователей");
                bool? result = smw.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    string mail = smw.OutputText;
                    if (!mail.IsNullOrEmptyString())
                    {
                        int successCount = 0;
                        foreach (var user in DataUsers.Where(u => u.IsSelected))
                        {
                            if (await handler.SendMailToUserAsync(user, mail))
                            {
                                successCount++;
                            }
                        }
                        notificationManager.Show("Результат", $"Сообщение отправлено {successCount}/{DataUsers.Count(u => u.IsSelected)} пользователям", NotificationType.Information);
                    }
                }
            }, () => IsHasSelectedDataUsers).ObservesProperty(() => IsHasSelectedDataUsers);
        }

        private void InitUserCommands(UserModel user)
        {
            user.OnCopyCommand = new DelegateCommand<string>((field) =>
            {
                try
                {
                    switch (field)
                    {
                        case "Username":
                            Clipboard.SetText("@" + user.Username);
                            break;

                        case "Id":
                            Clipboard.SetText(user.Id.ToString());
                            break;
                    }
                }
                catch (System.Exception)
                {
                }
            });
            user.AcceptAccessCommand = new DelegateCommand<UserModel>(async (user) =>
            {
                if (await handler.AcceptTelegramUserAsync(user))
                {
                    DataUsers.Add(user);
                    NotAcceptedUsers.Remove(user);
                }
            });
            user.MoveToBLCommand = new DelegateCommand<UserModel>(async (user) => await handler.MoveToBLUserAsync(user));
            user.MoveFromBLCommand = new DelegateCommand<UserModel>(async (user) => await handler.MoveFromBLUserAsync(user));
            user.SendMailCommand = new DelegateCommand<UserModel>(async (user) => await OnSendMailToSelectedUserAsync(user));
            user.ChangeCashCommand = new DelegateCommand<UserModel>(async (user) => await OnChangeUserCash(user));
        }

        private void UpdateNotAcceptedUsersCounter() => NotAcceptedUsersCount = NotAcceptedUsers.Count;

        private void UpdateDataUsersCounter() => DataUsersCount = DataUsers.Count;

        public void UpdateHasSelectedDataUsers() => IsHasSelectedDataUsers = DataUsers.Any(u => u.IsSelected);

        private async Task OnSendMailToSelectedUserAsync(UserModel user)
        {
            using SendMailWindow smw = new(user.Username);
            bool? result = smw.ShowDialog();
            if (result.HasValue && result.Value)
            {
                string mail = smw.OutputText;
                if (!mail.IsNullOrEmptyString())
                {
                    if (await handler.SendMailToUserAsync(user, mail))
                    {
                        notificationManager.Show("Успешно", "Сообщение успешно отправлено пользователю", NotificationType.Information);
                    }
                    else
                    {
                        notificationManager.Show("Ошибка", "Ошибка при отправке, возможно пользователь заблокировал бота", NotificationType.Error);
                    }
                }
            }
        }

        private async Task OnChangeUserCash(UserModel user)
        {
            using ChangeBalanceWindow cbw = new(user, config.Currency);
            bool? result = cbw.ShowDialog();
            if (result.HasValue && result.Value)
            {
                string updatedBalance = cbw.OutputText;
                bool sendNotification = cbw.SendNotification;

                if (updatedBalance != "none")
                {
                    double changeBalance = double.Parse(updatedBalance);
                    user.Balance += changeBalance;
                    if (await UsersController.PutUserAsync(user))
                    {
                        notificationManager.Show("Успешно", "Баланс пользователя был изменен", NotificationType.Information);
                        if (sendNotification)
                        {
                            await handler.SendBalanceInfoToUser(user, changeBalance > 0);
                        }
                    }
                }
            }
        }
    }
}