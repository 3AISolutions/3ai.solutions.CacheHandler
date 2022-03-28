namespace _3ai.solutions.CacheHandler
{
    public record CacheSettings
    {
        public int ShortTermExpiryMinutes { get; init; }
        public int LongTermExpiryMinutes { get; init; }
    }
}