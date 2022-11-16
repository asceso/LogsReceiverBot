using Models.Database;
using Prism.Events;

namespace AgregatorEvents
{
    public class UserUpdateEvent : PubSubEvent<KeyValuePair<string, UserModel>>
    { }
}