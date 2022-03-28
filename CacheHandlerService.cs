using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace _3ai.solutions.CacheHandler
{
    public class CacheHandlerService
    {
        public enum CacheExpiration
        {
            LongTerm,
            SortTerm,
            Never,
            LongTermAutoReset,
            SortTermAutoReset
        }

        public class CacheItem
        {
            public string Key { get; set; } = string.Empty;
            public CacheExpiration CacheExpiration { get; set; }
            public string ObjectName
            {
                get
                {
                    return Key.Split(":").First();
                }
            }
            public int PortalId
            {
                get
                {
                    if (int.TryParse(Key.Split(":").Last(), out int id))
                        return id;
                    else
                        return 0;
                }
            }
            public Func<IServiceScopeFactory, int, object>? Func { get; set; }
            public Func<IServiceScopeFactory, int, Task<object>>? FuncAsync { get; set; }
        }

        private ConcurrentDictionary<string, CacheItem> CacheItems { get; } = new();

        public ConcurrentQueue<string> CacheItemsToReset { get; } = new();
        public void AddCacheItemToReset(string key)
        {
            if (!CacheItemsToReset.Contains(key)) CacheItemsToReset.Enqueue(key);
        }

        public ConcurrentQueue<string> CacheItemsToClear { get; } = new();
        public void AddCacheItemToClear(string key)
        {
            if (!CacheItemsToClear.Contains(key)) CacheItemsToClear.Enqueue(key);
        }

        public IEnumerable<KeyValuePair<string, string>> GetCacheItemKeys()
        {
            return CacheItems.Select(ci => new KeyValuePair<string, string>(ci.Value.ObjectName, ci.Value.PortalId.ToString()));
        }

        private readonly IMemoryCache _memoryCache;
        private readonly CacheHandlerOptions _cacheSettings;
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ConcurrentBag<string> keys = new();

        public CacheHandlerService(IMemoryCache memoryCache, IOptions<CacheHandlerOptions> cacheSettings, IServiceScopeFactory scopeFactory)
        {
            _memoryCache = memoryCache;
            _cacheSettings = cacheSettings.Value;
            _scopeFactory = scopeFactory;
        }

        public IEnumerable<KeyValuePair<string, string>> Keys { get { return keys.Select(k => new KeyValuePair<string, string>(k.Split(":").First(), k.Split(":").Last())); } }

        public async Task Reset(string key)
        {
            if (CacheItems.TryGetValue(key, out var cacheItem))
            {
                object? itms = null;
                if (cacheItem.Func != null)
                    itms = cacheItem.Func(_scopeFactory, cacheItem.PortalId);
                else if (cacheItem.FuncAsync != null)
                    itms = await cacheItem.FuncAsync(_scopeFactory, cacheItem.PortalId);

                await Clear(key);

                if (itms is not null)
                {
                    MemoryCacheEntryOptions memoryCacheEntryOptions = new();
                    switch (cacheItem.CacheExpiration)
                    {
                        case CacheExpiration.LongTerm:
                            memoryCacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes));
                            break;
                        case CacheExpiration.SortTerm:
                            memoryCacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes));
                            break;
                        case CacheExpiration.LongTermAutoReset:
                            memoryCacheEntryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
                            {
                                var keyvalue = key.ToString();
                                if (!string.IsNullOrEmpty(keyvalue))
                                    AddCacheItemToReset(keyvalue);
                            }).AddExpirationToken(new CancellationChangeToken(
                                new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes)).Token));
                            break;
                        case CacheExpiration.SortTermAutoReset:
                            memoryCacheEntryOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
                            {
                                var keyvalue = key.ToString();
                                if (!string.IsNullOrEmpty(keyvalue))
                                    AddCacheItemToReset(keyvalue);
                            }).AddExpirationToken(new CancellationChangeToken(
                                new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes)).Token));
                            break;
                    }
                    _memoryCache.Set(key, itms, memoryCacheEntryOptions);
                }
            }
        }

        public Task Clear(string key)
        {
            _memoryCache.Remove(key);
            return Task.CompletedTask;
        }

        public TItem GetOrCreate<TItem>(string key, Func<IServiceScopeFactory, int, object> func,
                                            CacheExpiration cacheExpiration = CacheExpiration.Never,
                                            int portalId = 0)
        {
            return _memoryCache.GetOrCreate(key, cacheEntry =>
            {
                switch (cacheExpiration)
                {
                    case CacheExpiration.LongTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes);
                        break;
                    case CacheExpiration.SortTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes);
                        break;
                    case CacheExpiration.LongTermAutoReset:
                        cacheEntry.RegisterPostEvictionCallback((key, value, reason, state) =>
                        {
                            var keyvalue = key.ToString();
                            if (!string.IsNullOrEmpty(keyvalue))
                                AddCacheItemToReset(keyvalue);
                        }).AddExpirationToken(new CancellationChangeToken(
                            new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes)).Token));
                        break;
                    case CacheExpiration.SortTermAutoReset:
                        cacheEntry.RegisterPostEvictionCallback((key, value, reason, state) =>
                        {
                            var keyvalue = key.ToString();
                            if (!string.IsNullOrEmpty(keyvalue))
                                AddCacheItemToReset(keyvalue);
                        }).AddExpirationToken(new CancellationChangeToken(
                            new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes)).Token));
                        break;
                }
                if (!CacheItems.ContainsKey(key))
                    CacheItems.TryAdd(key, new CacheItem { Key = key, Func = func, CacheExpiration = cacheExpiration });
                return (TItem)func(_scopeFactory, portalId);
            });
        }

        public Task<TItem> GetOrCreateAsync<TItem>(string key, Func<IServiceScopeFactory, int, Task<object>> func,
                                                    CacheExpiration cacheExpiration = CacheExpiration.Never,
                                                    int portalId = 0)
        {
            return _memoryCache.GetOrCreateAsync(key, async cacheEntry =>
            {
                switch (cacheExpiration)
                {
                    case CacheExpiration.LongTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes);
                        break;
                    case CacheExpiration.SortTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes);
                        break;
                    case CacheExpiration.LongTermAutoReset:
                        cacheEntry.RegisterPostEvictionCallback((key, value, reason, state) =>
                        {
                            var keyvalue = key.ToString();
                            if (!string.IsNullOrEmpty(keyvalue))
                                AddCacheItemToReset(keyvalue);
                        }).AddExpirationToken(new CancellationChangeToken(
                            new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes)).Token));
                        break;
                    case CacheExpiration.SortTermAutoReset:
                        cacheEntry.RegisterPostEvictionCallback((key, value, reason, state) =>
                        {
                            var keyvalue = key.ToString();
                            if (!string.IsNullOrEmpty(keyvalue))
                                AddCacheItemToReset(keyvalue);
                        }).AddExpirationToken(new CancellationChangeToken(
                            new CancellationTokenSource(TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes)).Token));
                        break;
                }
                if (!CacheItems.ContainsKey(key))
                    CacheItems.TryAdd(key, new CacheItem { Key = key, FuncAsync = func, CacheExpiration = cacheExpiration });
                return (TItem)await func(_scopeFactory, portalId);
            });
        }

        public async Task<TItem> GetOrCreateAsync<TItem>(string key, Func<Task<TItem>> func, CacheExpiration cacheExpiration)
        {
            return await _memoryCache.GetOrCreateAsync(key, cacheEntry =>
            {
                switch (cacheExpiration)
                {
                    case CacheExpiration.LongTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.LongTermExpiryMinutes);
                        break;
                    case CacheExpiration.SortTerm:
                        cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.ShortTermExpiryMinutes);
                        break;
                    case CacheExpiration.Never:
                        break;
                }
                if (!keys.Contains(key))
                    keys.Add(key);

                return func();
            });
        }

        public void CheckChanges(Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker changeTracker)
        {
            var auditedEntities = changeTracker.Entries()
                                               .Where(p => p.State != EntityState.Unchanged);

            var allCacheKeys = CacheItemsToClear;
            List<string> keysToCacheClear = new();
            foreach (var entity in auditedEntities)
            {
                var entityName = entity.Metadata.Name.Split(".").Last();
                foreach (var key in allCacheKeys)
                {
                    if (key.StartsWith(entityName))
                        keysToCacheClear.Add(key);
                }
            }
            foreach (var key in keysToCacheClear.Distinct())
            {
                Clear(key);
            }
        }
    }
}