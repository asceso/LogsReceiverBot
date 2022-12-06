using Models.Database;
using Prism.Events;

namespace DatabaseEvents
{
    public class ManualCheckUpdateEvent : PubSubEvent<KeyValuePair<string, CpanelWhmCheckModel>>
    { }
}