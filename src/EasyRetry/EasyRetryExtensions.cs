using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyRetry
{
    /// <summary>
    /// Overloads of <see cref="IEasyRetry"/> that pass the 1-based attempt number and the policy's
    /// <see cref="RetryOptions.CancellationToken"/> to the delegate. They are built purely on the interface,
    /// so they work with any implementation.
    /// </summary>
    public static class EasyRetryExtensions
    {
        /// <summary>
        /// Invokes <paramref name="func"/> with the 1-based attempt number and the policy's cancellation token
        /// until it succeeds or the policy gives up, returning its result.
        /// </summary>
        /// <param name="easyRetry">The retry policy executor.</param>
        /// <param name="func">The asynchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        /// <typeparam name="T">The result type of the operation.</typeparam>
        /// <returns>The result of the first successful invocation.</returns>
        public static Task<T> Retry<T>(this IEasyRetry easyRetry, Func<int, CancellationToken, Task<T>> func,
            RetryOptions? retryOptions = null)
        {
            if (easyRetry == null) throw new ArgumentNullException(nameof(easyRetry));
            if (func == null) throw new ArgumentNullException(nameof(func));
            var options = retryOptions ?? new RetryOptions();
            var attempt = 0;
            return easyRetry.Retry(() => func(++attempt, options.CancellationToken), options);
        }

        /// <summary>
        /// Invokes <paramref name="func"/> with the 1-based attempt number and the policy's cancellation token
        /// until it succeeds or the policy gives up.
        /// </summary>
        /// <param name="easyRetry">The retry policy executor.</param>
        /// <param name="func">The asynchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        /// <returns>A task that completes when an invocation succeeds.</returns>
        public static Task Retry(this IEasyRetry easyRetry, Func<int, CancellationToken, Task> func,
            RetryOptions? retryOptions = null)
        {
            if (easyRetry == null) throw new ArgumentNullException(nameof(easyRetry));
            if (func == null) throw new ArgumentNullException(nameof(func));
            var options = retryOptions ?? new RetryOptions();
            var attempt = 0;
            return easyRetry.Retry(() => func(++attempt, options.CancellationToken), options);
        }

        /// <summary>
        /// Invokes <paramref name="action"/> synchronously with the 1-based attempt number and the policy's
        /// cancellation token until it succeeds or the policy gives up.
        /// </summary>
        /// <param name="easyRetry">The retry policy executor.</param>
        /// <param name="action">The synchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        public static void Retry(this IEasyRetry easyRetry, Action<int, CancellationToken> action,
            RetryOptions? retryOptions = null)
        {
            if (easyRetry == null) throw new ArgumentNullException(nameof(easyRetry));
            if (action == null) throw new ArgumentNullException(nameof(action));
            var options = retryOptions ?? new RetryOptions();
            var attempt = 0;
            easyRetry.Retry(() => action(++attempt, options.CancellationToken), options);
        }
    }
}
