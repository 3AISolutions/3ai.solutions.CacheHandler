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

        public void AddCacheItemToReset(string key)
        {
            if (!_cacheItemsToReset.Contains(key)) _cacheItemsToReset.Enqueue(key);
        }

        public string[] GetCacheKeysToReset()
        {
            return _cacheItemsToReset.ToArray();
        }

        public IEnumerable<string> CacheKeys
        {
            get
            {
                return _cacheItems.Select(c => c.Key);
            }
        }

        private readonly IMemoryCache _memoryCache;
        private readonly CacheHandlerOptions _cacheSettings;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<string, CacheItem> _cacheItems;
        private readonly ConcurrentQueue<string> _cacheItemsToReset;

        public CacheHandlerService(IMemoryCache memoryCache, IOptions<CacheHandlerOptions> cacheSettings, IServiceScopeFactory scopeFactory)
        {
            _memoryCache = memoryCache;
            _cacheSettings = cacheSettings.Value;
            _scopeFactory = scopeFactory;
            _cacheItems = new();
            _cacheItemsToReset = new();
        }

        private Task Clear(string key)
        {
            _memoryCache.Remove(key);
            return Task.CompletedTask;
        }

        public async Task Reset(string key)
        {
            if (_cacheItems.TryGetValue(key, out var cacheItem))
            {
                object? itms = null;
                if (cacheItem.Func != null)
                    itms = cacheItem.Func(_scopeFactory, cacheItem.Params);
                else if (cacheItem.FuncAsync != null)
                    itms = await cacheItem.FuncAsync(_scopeFactory, cacheItem.Params);

                await Clear(key);

                if (itms is not null)
                {
                    var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheItem.CacheExpiration);
                    _memoryCache.Set(key, itms, memoryCacheEntryOptions);
                }
            }
        }

        public TItem GetOrCreate<TItem>(string key, Func<IServiceScopeFactory, object[], object> func,
                                        List<string>? relatedKeys = null,
                                        CacheExpiration cacheExpiration = CacheExpiration.Never,
                                        params object[] paramArray)
        {
            return _memoryCache.GetOrCreate(key, cacheEntry =>
            {
                var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheExpiration);
                cacheEntry.SetOptions(memoryCacheEntryOptions);

                if (!_cacheItems.ContainsKey(key))
                    _cacheItems.TryAdd(key, new CacheItem(key, relatedKeys, cacheExpiration, func, paramArray));
                return (TItem)func(_scopeFactory, paramArray);
            });
        }

        public Task<TItem> GetOrCreateAsync<TItem>(string key, Func<IServiceScopeFactory, object[], Task<object>> funcAsync,
                                                   List<string>? relatedKeys = null,
                                                   CacheExpiration cacheExpiration = CacheExpiration.Never,
                                                   params object[] paramArray)
        {
            return _memoryCache.GetOrCreateAsync(key, async cacheEntry =>
            {
                var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheExpiration);
                cacheEntry.SetOptions(memoryCacheEntryOptions);

                if (!_cacheItems.ContainsKey(key))
                    _cacheItems.TryAdd(key, new CacheItem(key, relatedKeys, cacheExpiration, funcAsync, paramArray));
                return (TItem)await funcAsync(_scopeFactory, paramArray);
            });
        }

        private MemoryCacheEntryOptions CreateCacheEntryOptions(CacheExpiration cacheExpiration)
        {
            MemoryCacheEntryOptions memoryCacheEntryOptions = new();
            switch (cacheExpiration)
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
            return memoryCacheEntryOptions;
        }

        public void CheckChanges(Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker changeTracker)
        {
            var auditedEntities = changeTracker.Entries()
                                               .Where(p => p.State != EntityState.Unchanged);

            List<string> keysToCacheClear = new();
            foreach (var entity in auditedEntities)
            {
                var entityName = entity.Metadata.Name.Split(".").Last();
                foreach (var key in CacheKeys)
                {
                    if (key.StartsWith(entityName))
                        keysToCacheClear.Add(key);
                }
            }
            foreach (var key in keysToCacheClear.Distinct())
            {
                AddCacheItemToReset(key);
                //Clear(key);
            }
        }
    }
}