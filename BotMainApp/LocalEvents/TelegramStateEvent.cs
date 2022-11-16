using BotMainApp.ViewModels;
using Prism.Events;

namespace BotMainApp.Events
{
    public class TelegramStateEvent : PubSubEvent<TelegramStateModel>
    { }
}