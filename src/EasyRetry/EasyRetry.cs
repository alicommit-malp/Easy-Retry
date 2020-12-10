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

        private void LogException(int currentRetry, Exception ex)
        {
            _logger?.LogInformation(
                $"Attempt {currentRetry} has failed. " +
                $"see the inner exception for the details ----> " +
                $"Message: {ex.Message} " +
                $"Stack: {ex.StackTrace} ");
        }

        private async Task WaitBeforeRetry(int currentRetry, RetryOptions retryOptions)
        {
            if (currentRetry > 1 && retryOptions.EnableLogging)
                _logger?.LogInformation(
                    $"Retrying <Attempt:{currentRetry}> " +
                    $"Options:{retryOptions.Attempts}" +
                    $"/{retryOptions.EnableLogging}" +
                    $"/{retryOptions.DelayBetweenRetries}" +
                    $"/{retryOptions.DelayBeforeFirstTry}");
            await Task.Delay(retryOptions.DelayBeforeFirstTry);
        }

        /// <summary>
        /// Will retry the given <see cref="Func{TResult}"/> asynchronously with result <see cref="T"/>
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
                    await WaitBeforeRetry(currentRetry, retryOptions);
                    return await func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging) LogException(currentRetry,ex);

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
        /// Will retry the given <see cref="Func{TResult}"/> asynchronously without result
        /// </summary>
        /// <param name="func"><see cref="Func{TResult}"/></param>
        /// <param name="retryOptions"><see cref="RetryOptions"/></param>
        /// <returns><see cref="Task"/></returns>
        public async Task Retry(Func<Task> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    await WaitBeforeRetry(currentRetry, retryOptions);
                    await func.Invoke();
                    break;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging) LogException(currentRetry,ex);

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
        /// Will retry the given <see cref="Action"/> synchronously 
        /// </summary>
        /// <param name="action">The desired action to be retried with the specified <see cref="RetryOptions"/></param>
        /// <param name="retryOptions"><see cref="RetryOptions"/></param>
        /// <returns></returns>
        public void Retry(Action action, RetryOptions retryOptions = null)
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

                    action.Invoke();
                    break;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging) LogException(currentRetry,ex);

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
    }
}