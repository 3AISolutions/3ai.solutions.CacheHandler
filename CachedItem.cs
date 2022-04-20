namespace _3ai.solutions.CacheHandler
{
    internal class CachedItem<T>
    {
        internal CachedItem(T? value)
        {
            Value = value;
        }

        internal T? Value { get; init; }
    }
}