using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyRetry
{
    public class EasyRetry : IEasyRetry
    {
        private readonly ILogger _logger;

        public EasyRetry(ILogger<EasyRetry> logger)
        {
            _logger = logger;
        }

        public async Task<T> Retry<T>(Func<Task<T>> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger.LogInformation($"Retrying attempt {currentRetry} ... ");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    return await func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger.LogInformation(
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

        public T Retry<T>(Func<T> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger.LogInformation($"Retrying attempt {currentRetry} ... ");
                    Task.Delay(retryOptions.DelayBeforeFirstTry).GetAwaiter().GetResult();

                    return func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger.LogInformation(
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

        public async Task Retry(Func<Task> func, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        _logger.LogInformation($"Retrying attempt {currentRetry} ... ");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    await func.Invoke();
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        _logger.LogInformation(
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