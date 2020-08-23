using System;
using System.Collections.Generic;

namespace EasyRetry
{
    public class RetryOptions
    {
        public TimeSpan DelayBetweenRetries { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan DelayBeforeFirstTry { get; set; } = TimeSpan.FromSeconds(0);
        public int Attempts { get; set; } = 2;
        public bool EnableLogging { get; set; } = false;
        public List<Exception> DoNotRetryOnTheseExceptions { get; set; } = new List<Exception>();
    }
}