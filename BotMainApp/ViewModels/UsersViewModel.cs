using AgregatorEvents;
using BotMainApp.Telegram;
using DataAdapter.Controllers;
using Models.Database;
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
using System.Windows.Threading;

namespace BotMainApp.ViewModels
{
    public class UsersViewModel : BindableBase
    {
        private readonly bool isDebug = false;
        private readonly UpdateHandler handler;

        private bool isLoading;
        private int notAcceptedUsersCount;
        private int dataUsersCount;
        private ObservableCollection<UserModel> notAcceptedUsers;
        private ObservableCollection<UserModel> dataUsers;

        public bool IsLoading { get => isLoading; set => SetProperty(ref isLoading, value); }
        public int NotAcceptedUsersCount { get => notAcceptedUsersCount; set => SetProperty(ref notAcceptedUsersCount, value); }
        public int DataUsersCount { get => dataUsersCount; set => SetProperty(ref dataUsersCount, value); }
        public ObservableCollection<UserModel> NotAcceptedUsers { get => notAcceptedUsers; set => SetProperty(ref notAcceptedUsers, value); }
        public ObservableCollection<UserModel> DataUsers { get => dataUsers; set => SetProperty(ref dataUsers, value); }

        public DelegateCommand RefreshCommand { get; set; }

        public UsersViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            NotAcceptedUsers = new();
            DataUsers = new();
            NotAcceptedUsers.CollectionChanged += (s, e) => UpdateNotAcceptedUsersCounter();
            DataUsers.CollectionChanged += (s, e) => UpdateDataUsersCounter();
            handler = memory.GetItem<UpdateHandler>("Handler");
            aggregator.GetEvent<UserUpdateEvent>().Subscribe(OnUserUpdates);
            RefreshCommand = new DelegateCommand(async () => await LoadUsersAsync());
            RefreshCommand.Execute();
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
            catch (Exception ex)
            {
            }
        }

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
                foreach (UserModel user in cacheUsers.Where(u => u.IsAccepted).OrderBy(u => u.Id)) DataUsers.Add(user);
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

        private void UpdateNotAcceptedUsersCounter() => NotAcceptedUsersCount = NotAcceptedUsers.Count;

        private void UpdateDataUsersCounter() => DataUsersCount = DataUsers.Count;

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
        }
    }
}