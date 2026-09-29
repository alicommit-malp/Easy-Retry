using System;
using System.Threading.Tasks;

namespace EasyRetry
{
    /// <summary>
    /// Retries a delegate according to a <see cref="RetryOptions"/> policy.
    /// </summary>
    public interface IEasyRetry
    {
        /// <summary>
        /// Invokes <paramref name="func"/> until it succeeds or the policy gives up, returning its result.
        /// </summary>
        /// <param name="func">The asynchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        /// <typeparam name="T">The result type of the operation.</typeparam>
        /// <returns>The result of the first successful invocation.</returns>
        Task<T> Retry<T>(Func<Task<T>> func, RetryOptions? retryOptions = null);

        /// <summary>
        /// Invokes <paramref name="func"/> until it succeeds or the policy gives up.
        /// </summary>
        /// <param name="func">The asynchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        /// <returns>A task that completes when an invocation succeeds.</returns>
        Task Retry(Func<Task> func, RetryOptions? retryOptions = null);

        /// <summary>
        /// Invokes <paramref name="action"/> synchronously until it succeeds or the policy gives up.
        /// </summary>
        /// <param name="action">The synchronous operation to retry.</param>
        /// <param name="retryOptions">The retry policy; <c>null</c> uses the defaults.</param>
        void Retry(Action action, RetryOptions? retryOptions = null);
    }
}
