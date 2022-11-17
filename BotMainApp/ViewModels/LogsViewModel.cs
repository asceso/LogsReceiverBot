using BotMainApp.LocalEvents;
using BotMainApp.TelegramServices;
using Models.App;
using Notification.Wpf;
using Prism.Events;
using Prism.Mvvm;
using Services.Interfaces;

namespace BotMainApp.ViewModels
{
    public class LogsViewModel : BindableBase
    {
        private readonly NotificationManager notificationManager;
        private readonly ConfigModel config;
        private readonly IMemorySaver memory;
        private UpdateHandler handler;

        public LogsViewModel(IEventAggregator aggregator, IMemorySaver memory)
        {
            this.memory = memory;
            handler = memory.GetItem<UpdateHandler>("Handler");
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            config = memory.GetItem<ConfigModel>("Config");
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnBotRestart);
        }

        private void OnBotRestart() => handler = memory.GetItem<UpdateHandler>("Handler");
    }
}