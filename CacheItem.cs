using Microsoft.Extensions.DependencyInjection;
using static _3ai.solutions.CacheHandler.CacheHandlerService;

namespace _3ai.solutions.CacheHandler
{
    internal class CacheItem
    {

        public CacheItem(string key, List<string>? relatedKeys, CacheExpiration cacheExpiration, Func<IServiceScopeFactory, object[], object> func, object[] funcParams)
        {
            Key = key;
            if (relatedKeys is not null) RelatedKeys = relatedKeys;
            Func = func;
            Params = funcParams;
            CacheExpiration = cacheExpiration;
        }

        public CacheItem(string key, List<string>? relatedKeys, CacheExpiration cacheExpiration, Func<IServiceScopeFactory, object[], Task<object>> funcAsync, object[] funcParams)
        {
            Key = key;
            if (relatedKeys is not null) RelatedKeys = relatedKeys;
            FuncAsync = funcAsync;
            Params = funcParams;
            CacheExpiration = cacheExpiration;
        }

        public string Key { get; init; } = string.Empty;
        public List<string> RelatedKeys { get; init; } = new();
        public CacheExpiration CacheExpiration { get; init; }
        public object[] Params { get; init; } = Array.Empty<object>();
        public Func<IServiceScopeFactory, object[], object>? Func { get; init; }
        public Func<IServiceScopeFactory, object[], Task<object>>? FuncAsync { get; init; }
    }
}