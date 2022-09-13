using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace _3ai.solutions.CacheHandler
{
    public class CacheHandlerService
    {
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

        private void Clear(string key)
        {
            _memoryCache.Remove(key);
        }

        public async Task Reset(string key, bool resetDependancies = false)
        {
            if (_cacheItems.TryGetValue(key, out var cacheItem))
            {
                object? itms = null;
                if (cacheItem.Func != null)
                    itms = cacheItem.Func(_scopeFactory, cacheItem.Params);
                else if (cacheItem.FuncAsync != null)
                    itms = await cacheItem.FuncAsync(_scopeFactory, cacheItem.Params);

                Clear(key);

                if (itms is not null)
                {
                    var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheItem.CacheExpiration);
                    _memoryCache.Set(key, itms, memoryCacheEntryOptions);
                }

                if (resetDependancies)
                {
                    foreach (var item in cacheItem.RelatedKeys)
                    {
                        await Reset(item, true);
                    }
                }
            }
        }

        public TItem? Get<TItem>(string key)
        {
            if (_memoryCache.TryGetValue<TItem>(key, out var value))
                return value;
            return default;
        }

        public void Set<TItem>(string key, TItem value)
        {
            _memoryCache.Set(key, value);
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

        public TItem? GetOrCreateNullable<TItem>(string key, Func<object?> func, CacheExpiration cacheExpiration = CacheExpiration.Never)
        {
            return _memoryCache.GetOrCreate(key, cacheEntry =>
            {
                var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheExpiration);
                cacheEntry.SetOptions(memoryCacheEntryOptions);

                if (!_cacheItems.ContainsKey(key))
                    _cacheItems.TryAdd(key, new CacheItem());
                return new CachedItem<TItem>((TItem?)func());
            }).Value;
        }

        public async Task<TItem?> GetOrCreateNullableAsync<TItem>(string key, Func<Task<object?>> funcAsync, CacheExpiration cacheExpiration = CacheExpiration.Never, List<string>? relatedKeys = null)
        {
            return (await _memoryCache.GetOrCreateAsync(key, async cacheEntry =>
            {
                var memoryCacheEntryOptions = CreateCacheEntryOptions(cacheExpiration);
                cacheEntry.SetOptions(memoryCacheEntryOptions);

                if (!_cacheItems.ContainsKey(key))
                    _cacheItems.TryAdd(key, new CacheItem() { RelatedKeys = relatedKeys ?? new() });
                return new CachedItem<TItem>((TItem?)await funcAsync());
            })).Value;
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

        public void AddCacheItemToReset(string key)
        {
            if (!_cacheItemsToReset.Contains(key)) _cacheItemsToReset.Enqueue(key);
        }

        public bool GetCacheKeyToReset(out string? key)
        {
            return _cacheItemsToReset.TryDequeue(out key);
        }

        public IEnumerable<string> CacheKeys
        {
            get
            {
                return _cacheItems.Select(c => c.Key);
            }
        }

        public IEnumerable<IEnumerable<string>> GetKeysWithRelated()
        {
            return _cacheItems.Select(c => c.Value.RelatedKeys.Concat(new[] { c.Key }));
        }
    }
}