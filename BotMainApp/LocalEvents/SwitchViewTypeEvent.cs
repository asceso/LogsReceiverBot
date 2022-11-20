using Prism.Events;
using static Models.Enums.ViewsPayload;

namespace BotMainApp.LocalEvents
{
    public class SwitchViewTypeEvent : PubSubEvent<ViewTypes>
    { }
}