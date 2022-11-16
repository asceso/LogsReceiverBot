using BotMainApp.Telegram;
using DataAdapter.Controllers;
using Models.Database;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Unity;

namespace BotMainApp.ViewModels
{
    public class UsersViewModel : BindableBase
    {
        private readonly bool isDebug = false;
        private readonly IUnityContainer container;

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

        public UsersViewModel(IUnityContainer container)
        {
            this.container = container;
            NotAcceptedUsers = new();
            DataUsers = new();
            RefreshCommand = new DelegateCommand(async () => await LoadUsersAsync());
            Task.Run(async () => await LoadUsersAsync());
        }

        private async Task LoadUsersAsync()
        {
            NotAcceptedUsers.Clear();
            DataUsers.Clear();
            UpdateUsersCounters();
            IsLoading = true;
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
            NotAcceptedUsers = new(cacheUsers.Where(u => !u.IsAccepted));
            DataUsers = new(cacheUsers.Where(u => u.IsAccepted).OrderBy(u => u.Id));
            UpdateUsersCounters();
            foreach (UserModel user in NotAcceptedUsers) InitUserCommands(user);
            foreach (UserModel user in DataUsers) InitUserCommands(user);
            RaisePropertyChanged(nameof(NotAcceptedUsers));
            RaisePropertyChanged(nameof(DataUsers));
            IsLoading = false;
        }

        private void UpdateUsersCounters()
        {
            NotAcceptedUsersCount = NotAcceptedUsers.Count;
            DataUsersCount = DataUsers.Count;
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
                if (await UpdateHandler.AcceptTelegramUserAsync(user))
                {
                    DataUsers.Add(user);
                    NotAcceptedUsers.Remove(user);
                    UpdateUsersCounters();
                }
            });
            user.MoveToBLCommand = new DelegateCommand<UserModel>(async (user) => await UpdateHandler.MoveToBLUserAsync(user));
            user.MoveFromBLCommand = new DelegateCommand<UserModel>(async (user) => await UpdateHandler.MoveFromBLUserAsync(user));
        }
    }
}