using Models.Database;
using Prism.Events;

namespace DatabaseEvents
{
    public class PayoutUpdateEvent : PubSubEvent<KeyValuePair<string, PayoutModel>>
    { }
}