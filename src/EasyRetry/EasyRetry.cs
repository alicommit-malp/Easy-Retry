using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyRetry
{
    public class EasyRetry : IEasyRetry
    {
        private readonly ILogger _logger;

        /// <summary>
        /// For normal instantiation 
        /// </summary>
        public EasyRetry()
        {
        }
        
        /// <summary>
        /// For DI instantiation 
        /// </summary>
        /// <param name="logger"></param>
        public EasyRetry(ILogger<EasyRetry> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Will retry the given <see cref="Func{TResult}"/> asynchronously
        /// </summary>
        /// <param name="func"><see cref="Func{TResult}"/></param>
        /// <param name="retryOptions"><see cref="RetryOptions"/></param>
        /// <typeparam name="T"></typeparam>
        /// <returns><see cref="T"/>Type</returns>
        public async Task<T> Retry<T>(Func<Task<T>> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger?.LogInformation($"Retrying attempt {currentRetry} ... ");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    return await func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger?.LogInformation(
                            $"Attempt {currentRetry} has failed. " +
                            $"see the inner exception for the details ----> " +
                            $"Message: {ex.Message} " +
                            $"Stack: {ex.StackTrace} ");

                    currentRetry++;
                    if (currentRetry > retryOptions.Attempts ||
                        retryOptions.DoNotRetryOnTheseExceptionTypes.Any(z => z == ex.GetType()))
                    {
                        throw;
                    }
                }

                await Task.Delay(retryOptions.DelayBetweenRetries);
            }
        }

        /// <summary>
        /// Will retry the given <see cref="Func{TResult}"/>
        /// </summary>
        /// <param name="func"><see cref="Func{TResult}"/></param>
        /// <param name="retryOptions"><see cref="RetryOptions"/></param>
        /// <typeparam name="T"></typeparam>
        /// <returns><see cref="T"/>Type</returns>
        public T Retry<T>(Func<T> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger?.LogInformation($"Retrying attempt {currentRetry} ... ");
                    Task.Delay(retryOptions.DelayBeforeFirstTry).GetAwaiter().GetResult();

                    return func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger?.LogInformation(
                            $"Attempt {currentRetry} has failed. " +
                            $"see the inner exception for the details ----> " +
                            $"Message: {ex.Message} " +
                            $"Stack: {ex.StackTrace} ");

                    currentRetry++;
                    if (currentRetry > retryOptions.Attempts ||
                        retryOptions.DoNotRetryOnTheseExceptionTypes.Any(z => z == ex.GetType()))
                    {
                        throw;
                    }
                }

                Task.Delay(retryOptions.DelayBetweenRetries).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Will retry the given <see cref="Action"/>
        /// </summary>
        /// <param name="action">The desired action to be retried with the specified <see cref="RetryOptions"/></param>
        /// <param name="retryOptions"><see cref="RetryOptions"/></param>
        /// <returns></returns>
        public async Task Retry(Action action, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger?.LogInformation($"Retrying attempt {currentRetry} ... ");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    action.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger?.LogInformation(
                            $"Attempt {currentRetry} has failed. " +
                            $"see the inner exception for the details ----> " +
                            $"Message: {ex.Message} " +
                            $"Stack: {ex.StackTrace} ");

                    currentRetry++;
                    if (currentRetry > retryOptions.Attempts ||
                        retryOptions.DoNotRetryOnTheseExceptionTypes.Any(z => z == ex.GetType()))
                    {
                        throw;
                    }
                }

                await Task.Delay(retryOptions.DelayBetweenRetries);
            }
        }
    }
}