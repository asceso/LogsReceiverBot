using Models.Database;
using Prism.Events;

namespace DatabaseEvents
{
    public class WpLoginCheckUpdateEvent : PubSubEvent<KeyValuePair<string, WpLoginCheckModel>>
    { }
}