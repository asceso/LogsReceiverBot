using Models.Database;
using Prism.Events;

namespace DatabaseEvents
{
    public class CpanelWhmCheckUpdateEvent : PubSubEvent<KeyValuePair<string, CpanelWhmCheckModel>>
    { }
}