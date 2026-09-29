using System;
using System.Collections.Generic;
using System.Threading;

namespace EasyRetry
{
    /// <summary>
    /// Describes how <see cref="EasyRetry"/> retries an operation.
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// Base delay between two consecutive attempts. Default: 5 seconds.
        /// </summary>
        public TimeSpan DelayBetweenRetries { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Delay applied once, before the first attempt. Default: zero.
        /// </summary>
        public TimeSpan DelayBeforeFirstTry { get; set; } = TimeSpan.FromSeconds(0);

        /// <summary>
        /// Total number of invocations, including the first one. Default: 2.
        /// </summary>
        public int Attempts { get; set; } = 2;

        /// <summary>
        /// When <c>true</c>, failed attempts are logged through the configured logger. Default: <c>false</c>.
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// Exception types (including derived types) that stop retrying immediately.
        /// </summary>
        public List<Type> DoNotRetryOnTheseExceptionTypes { get; set; } = new List<Type>();

        /// <summary>
        /// Optional predicate consulted after each failed attempt with the exception and the 1-based
        /// number of the attempt that failed. Returning <c>false</c> stops retrying and the exception propagates.
        /// </summary>
        public Func<Exception, int, bool>? ShouldRetry { get; set; }

        /// <summary>
        /// Token that aborts waiting and prevents further attempts. Default: <see cref="CancellationToken.None"/>.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// How the delay grows between attempts. Default: <see cref="RetryBackoff.Constant"/>.
        /// </summary>
        public RetryBackoff Backoff { get; set; } = RetryBackoff.Constant;

        /// <summary>
        /// Upper bound for a single computed delay. Default: no bound.
        /// </summary>
        public TimeSpan? MaxDelay { get; set; }

        /// <summary>
        /// When <c>true</c>, each computed delay is randomised within +/-20%. Default: <c>false</c>.
        /// </summary>
        public bool UseJitter { get; set; } = false;

        /// <summary>
        /// Called with the result of a successful invocation of the <c>Func&lt;Task&lt;T&gt;&gt;</c> overload.
        /// Returning <c>true</c> treats the attempt as failed. Ignored by the overloads without a result.
        /// </summary>
        public Func<object?, bool>? RetryOnResult { get; set; }

        /// <summary>
        /// Upper bound for the total time spent inside a <c>Retry</c> call, measured from its entry. Once a
        /// failed attempt plus the next delay would exceed it, no further attempt is made and the call gives up
        /// as if the attempts were exhausted. An attempt already in flight is never aborted. Default: no bound.
        /// </summary>
        public TimeSpan? MaxTotalDuration { get; set; }

        /// <summary>
        /// Invoked once per retry, after the attempt is logged and before the delay is waited, with the exception
        /// of the failed attempt (<c>null</c> for a result-based retry), the 1-based number of the failed attempt and
        /// the delay about to be waited. Not gated by <see cref="EnableLogging"/> and not invoked when giving up.
        /// An exception thrown by the hook propagates immediately and stops retrying. Default: none.
        /// </summary>
        public Action<Exception?, int, TimeSpan>? OnRetry { get; set; }

        /// <summary>
        /// Called after each failure that is retried, with the exception (<c>null</c> for a result-based retry) and
        /// the 1-based number of the failed attempt. A non-<c>null</c> return replaces the computed backoff delay for
        /// that retry; <see cref="UseJitter"/> is not applied to it but <see cref="MaxDelay"/> still caps it. A negative
        /// return throws <see cref="ArgumentOutOfRangeException"/>. Returning <c>null</c> keeps the computed delay.
        /// Default: none.
        /// </summary>
        public Func<Exception?, int, TimeSpan?>? DelayOverride { get; set; }

        /// <summary>
        /// Creates a policy that waits the same <paramref name="delay"/> before every retry.
        /// Only <see cref="Attempts"/>, <see cref="DelayBetweenRetries"/> and <see cref="Backoff"/> are set;
        /// nothing is validated until the policy is used.
        /// </summary>
        /// <param name="attempts">Total number of invocations, including the first one.</param>
        /// <param name="delay">Delay between two consecutive attempts.</param>
        /// <returns>A new <see cref="RetryOptions"/>.</returns>
        public static RetryOptions Constant(int attempts, TimeSpan delay)
        {
            return new RetryOptions
            {
                Attempts = attempts,
                DelayBetweenRetries = delay,
                Backoff = RetryBackoff.Constant
            };
        }

        /// <summary>
        /// Creates a policy whose retry <c>n</c> waits <c>n</c> times <paramref name="baseDelay"/>.
        /// Only <see cref="Attempts"/>, <see cref="DelayBetweenRetries"/> and <see cref="Backoff"/> are set;
        /// nothing is validated until the policy is used.
        /// </summary>
        /// <param name="attempts">Total number of invocations, including the first one.</param>
        /// <param name="baseDelay">Base delay multiplied by the retry number.</param>
        /// <returns>A new <see cref="RetryOptions"/>.</returns>
        public static RetryOptions Linear(int attempts, TimeSpan baseDelay)
        {
            return new RetryOptions
            {
                Attempts = attempts,
                DelayBetweenRetries = baseDelay,
                Backoff = RetryBackoff.Linear
            };
        }

        /// <summary>
        /// Creates a policy whose retry <c>n</c> waits <c>2^(n-1)</c> times <paramref name="baseDelay"/>, optionally
        /// capped and jittered. Only <see cref="Attempts"/>, <see cref="DelayBetweenRetries"/>, <see cref="Backoff"/>,
        /// <see cref="MaxDelay"/> and <see cref="UseJitter"/> are set; nothing is validated until the policy is used.
        /// </summary>
        /// <param name="attempts">Total number of invocations, including the first one.</param>
        /// <param name="baseDelay">Delay before the first retry; doubled on each further retry.</param>
        /// <param name="maxDelay">Upper bound for a single delay; <c>null</c> for no bound.</param>
        /// <param name="useJitter">Whether each delay is randomised within +/-20%.</param>
        /// <returns>A new <see cref="RetryOptions"/>.</returns>
        public static RetryOptions Exponential(int attempts, TimeSpan baseDelay, TimeSpan? maxDelay = null, bool useJitter = true)
        {
            return new RetryOptions
            {
                Attempts = attempts,
                DelayBetweenRetries = baseDelay,
                Backoff = RetryBackoff.Exponential,
                MaxDelay = maxDelay,
                UseJitter = useJitter
            };
        }
    }
}
