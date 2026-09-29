using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: InternalsVisibleTo("EasyRetryTest")]

namespace EasyRetry
{
    /// <summary>
    /// Default <see cref="IEasyRetry"/> implementation.
    /// </summary>
    public class EasyRetry : IEasyRetry
    {
        private const double MaxDelayMilliseconds = int.MaxValue;
        private static readonly Random JitterSource = new Random();
        private static readonly object JitterLock = new object();

        private readonly ILogger<EasyRetry> _logger;

        /// <summary>
        /// A shared instance without logging.
        /// </summary>
        public static IEasyRetry Default { get; } = new EasyRetry();

        /// <summary>
        /// Creates an instance that does not log.
        /// </summary>
        public EasyRetry() : this(NullLogger<EasyRetry>.Instance)
        {
        }

        /// <summary>
        /// Creates an instance that logs failed attempts to <paramref name="logger"/> when
        /// <see cref="RetryOptions.EnableLogging"/> is set.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        public EasyRetry(ILogger<EasyRetry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<T> Retry<T>(Func<Task<T>> func, RetryOptions? retryOptions = null)
        {
            var stopwatch = Stopwatch.StartNew();
            if (func == null) throw new ArgumentNullException(nameof(func));
            var options = retryOptions ?? new RetryOptions();
            var doNotRetry = Validate(options);
            return RetryAsyncCore(func, options, doNotRetry, stopwatch, checkResult: true);
        }

        /// <inheritdoc />
        public Task Retry(Func<Task> func, RetryOptions? retryOptions = null)
        {
            var stopwatch = Stopwatch.StartNew();
            if (func == null) throw new ArgumentNullException(nameof(func));
            var options = retryOptions ?? new RetryOptions();
            var doNotRetry = Validate(options);
            return RetryAsyncCore(async () =>
            {
                await func().ConfigureAwait(false);
                return true;
            }, options, doNotRetry, stopwatch, checkResult: false);
        }

        /// <inheritdoc />
        public void Retry(Action action, RetryOptions? retryOptions = null)
        {
            var stopwatch = Stopwatch.StartNew();
            if (action == null) throw new ArgumentNullException(nameof(action));
            var options = retryOptions ?? new RetryOptions();
            var doNotRetry = Validate(options);
            options.CancellationToken.ThrowIfCancellationRequested();
            Sleep(options.DelayBeforeFirstTry, options.CancellationToken);
            var exceptions = new List<Exception>();
            var delay = TimeSpan.Zero;

            for (var attempt = 1; attempt <= options.Attempts; attempt++)
            {
                if (attempt > 1)
                {
                    Sleep(delay, options.CancellationToken);
                }

                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    if (!TryPlanRetry(ex, attempt, options, doNotRetry, stopwatch.Elapsed, out delay))
                    {
                        GiveUp(ex, attempt, options, exceptions);
                        throw;
                    }

                    LogRetry(ex, attempt, options, delay);
                    options.OnRetry?.Invoke(ex, attempt, delay);
                }
            }
        }

        private async Task<T> RetryAsyncCore<T>(Func<Task<T>> func, RetryOptions options, Type[] doNotRetry, Stopwatch stopwatch, bool checkResult)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            await DelayAsync(options.DelayBeforeFirstTry, options.CancellationToken).ConfigureAwait(false);
            var exceptions = new List<Exception>();
            var delay = TimeSpan.Zero;

            for (var attempt = 1; attempt <= options.Attempts; attempt++)
            {
                if (attempt > 1)
                {
                    await DelayAsync(delay, options.CancellationToken).ConfigureAwait(false);
                }

                T result;
                try
                {
                    result = await func().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    if (!TryPlanRetry(ex, attempt, options, doNotRetry, stopwatch.Elapsed, out delay))
                    {
                        GiveUp(ex, attempt, options, exceptions);
                        throw;
                    }

                    LogRetry(ex, attempt, options, delay);
                    options.OnRetry?.Invoke(ex, attempt, delay);
                    continue;
                }

                if (!checkResult || !RequiresRetry(result, options))
                {
                    return result;
                }

                if (!TryPlanRetry(null, attempt, options, doNotRetry, stopwatch.Elapsed, out delay))
                {
                    GiveUpOnResult(attempt, options);
                    return result;
                }

                LogRetry(null, attempt, options, delay);
                options.OnRetry?.Invoke(null, attempt, delay);
            }

            throw new InvalidOperationException("Retry loop exited without a result.");
        }

        private static Type[] Validate(RetryOptions options)
        {
            if (options.Attempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Attempts), options.Attempts, "Attempts must be at least 1.");
            }

            if (options.DelayBetweenRetries < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.DelayBetweenRetries), options.DelayBetweenRetries, "Delay must not be negative.");
            }

            if (options.DelayBeforeFirstTry < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.DelayBeforeFirstTry), options.DelayBeforeFirstTry, "Delay must not be negative.");
            }

            if (options.MaxDelay.HasValue && options.MaxDelay.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxDelay), options.MaxDelay, "Delay must not be negative.");
            }

            if (options.MaxTotalDuration.HasValue && options.MaxTotalDuration.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxTotalDuration), options.MaxTotalDuration, "Duration must not be negative.");
            }

            if (options.DoNotRetryOnTheseExceptionTypes == null)
            {
                throw new ArgumentException("DoNotRetryOnTheseExceptionTypes must not be null.", nameof(options.DoNotRetryOnTheseExceptionTypes));
            }

            return options.DoNotRetryOnTheseExceptionTypes.ToArray();
        }

        private static bool ShouldRetry(Exception ex, int attempt, RetryOptions options, Type[] doNotRetry)
        {
            return attempt < options.Attempts
                   && !(ex is OperationCanceledException && options.CancellationToken.IsCancellationRequested)
                   && !doNotRetry.Any(t => t.IsInstanceOfType(ex))
                   && (options.ShouldRetry?.Invoke(ex, attempt) ?? true);
        }

        private static bool RequiresRetry<T>(T result, RetryOptions options)
        {
            return options.RetryOnResult != null && options.RetryOnResult(result);
        }

        /// <summary>
        /// Decides whether the failed <paramref name="attempt"/> is retried. <paramref name="ex"/> is <c>null</c>
        /// for a result-based failure, to which the exception filters do not apply. When it returns <c>true</c>,
        /// <paramref name="delay"/> is the wait before the next attempt.
        /// </summary>
        private static bool TryPlanRetry(Exception? ex, int attempt, RetryOptions options, Type[] doNotRetry,
            TimeSpan elapsed, out TimeSpan delay)
        {
            delay = TimeSpan.Zero;
            var retryable = ex == null ? attempt < options.Attempts : ShouldRetry(ex, attempt, options, doNotRetry);
            if (!retryable)
            {
                return false;
            }

            delay = ResolveDelay(ex, attempt, options);
            return WithinBudget(elapsed, delay, options);
        }

        private static bool WithinBudget(TimeSpan elapsed, TimeSpan delay, RetryOptions options)
        {
            return !options.MaxTotalDuration.HasValue || elapsed + delay <= options.MaxTotalDuration.Value;
        }

        private void LogRetry(Exception? ex, int attempt, RetryOptions options, TimeSpan delay)
        {
            if (!options.EnableLogging)
            {
                return;
            }

            if (ex != null)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} of {MaxAttempts} failed; retrying in {Delay}",
                    attempt, options.Attempts, delay);
            }
            else
            {
                _logger.LogWarning("Attempt {Attempt} of {MaxAttempts} returned a result that requires retry; retrying in {Delay}",
                    attempt, options.Attempts, delay);
            }
        }

        private void GiveUpOnResult(int attempt, RetryOptions options)
        {
            if (options.EnableLogging)
            {
                _logger.LogWarning("Attempt {Attempt} of {MaxAttempts} returned a result that requires retry; giving up and returning it",
                    attempt, options.Attempts);
            }
        }

        private void GiveUp(Exception ex, int attempt, RetryOptions options, List<Exception> exceptions)
        {
            if (options.EnableLogging)
            {
                _logger.LogError(ex, "Attempt {Attempt} of {MaxAttempts} failed; giving up",
                    attempt, options.Attempts);
            }

            if (ex.Data.IsReadOnly)
            {
                return;
            }

            ex.Data[RetryDataKeys.Attempts] = attempt;
            ex.Data[RetryDataKeys.Exceptions] = (IReadOnlyList<Exception>)exceptions.AsReadOnly();
        }

        internal static TimeSpan GetDelay(int failedAttempt, RetryOptions options)
        {
            var baseMs = options.DelayBetweenRetries.TotalMilliseconds;
            double ms;
            switch (options.Backoff)
            {
                case RetryBackoff.Linear:
                    ms = baseMs * failedAttempt;
                    break;
                case RetryBackoff.Exponential:
                    ms = baseMs * Math.Pow(2, failedAttempt - 1);
                    break;
                default:
                    ms = baseMs;
                    break;
            }

            if (options.UseJitter && ms > 0)
            {
                ms *= NextJitterFactor();
            }

            var cap = options.MaxDelay.HasValue
                ? Math.Min(options.MaxDelay.Value.TotalMilliseconds, MaxDelayMilliseconds)
                : MaxDelayMilliseconds;
            ms = Math.Max(0, Math.Min(ms, cap));
            return TimeSpan.FromMilliseconds(ms);
        }

        /// <summary>
        /// The delay before the retry that follows <paramref name="failedAttempt"/>: the caller's
        /// <see cref="RetryOptions.DelayOverride"/> when it returns a value (capped by <see cref="RetryOptions.MaxDelay"/>,
        /// never jittered), otherwise <see cref="GetDelay"/>.
        /// </summary>
        internal static TimeSpan ResolveDelay(Exception? ex, int failedAttempt, RetryOptions options)
        {
            var overridden = options.DelayOverride?.Invoke(ex, failedAttempt);
            if (!overridden.HasValue)
            {
                return GetDelay(failedAttempt, options);
            }

            var delay = overridden.Value;
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.DelayOverride), delay, "Delay must not be negative.");
            }

            var cap = TimeSpan.FromMilliseconds(MaxDelayMilliseconds);
            if (options.MaxDelay.HasValue && options.MaxDelay.Value < cap)
            {
                cap = options.MaxDelay.Value;
            }

            return delay < cap ? delay : cap;
        }

        private static double NextJitterFactor()
        {
            lock (JitterLock)
            {
                return 0.8 + JitterSource.NextDouble() * 0.4;
            }
        }

        private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return delay > TimeSpan.Zero ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
        }

        private static void Sleep(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            cancellationToken.WaitHandle.WaitOne(delay);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
