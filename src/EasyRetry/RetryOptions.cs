using System;

namespace EasyRetry
{
    public class RetryOptions
    {
        public TimeSpan DelayBetweenRetries { get; set; } = TimeSpan.FromSeconds(5);
        public int Attempts { get; set; } = 2;
    }
}