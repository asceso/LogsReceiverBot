using Services.Interfaces;
using System.Runtime.Caching;

namespace Services.Implementation
{
    public class MemorySaver : IMemorySaver
    {
        private readonly CacheItemPolicy policy;
        private static MemoryCache cache;

        public MemorySaver()
        {
            cache = MemoryCache.Default;
            policy = new()
            {
                SlidingExpiration = TimeSpan.FromMinutes(0)
            };
        }

        public void StoreItem<TData>(string alias, TData item) => cache.Set(alias, item, policy);

        public TData GetItem<TData>(string alias)
        {
            object data = cache.Get(alias);
            if (data is TData item)
            {
                return item;
            }
            return default;
        }

        public bool ItemExist(string alias) => cache.Any(c => c.Key == alias);

        public void RemoveItem(string alias) => cache.Remove(alias);
    }
}