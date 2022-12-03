using Models.Database;
using Prism.Events;

namespace DatabaseEvents
{
    public class CookieUpdateEvent : PubSubEvent<KeyValuePair<string, CookieModel>>
    { }
}