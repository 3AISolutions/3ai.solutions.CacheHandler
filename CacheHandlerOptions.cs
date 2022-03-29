namespace _3ai.solutions.CacheHandler
{
    public record CacheHandlerOptions
    {
        public int ShortTermExpiryMinutes { get; init; } = 1;
        public int LongTermExpiryMinutes { get; init; } = 60;
        public int BackgroundWaitTimeMilliseconds { get; init; } = 1000;
        public bool UseHostedService { get; init; } = false;
    }
}