using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace EasyRetry
{
    public static class EasyRetryTaskExtension
    {
        /// <summary>
        /// An extension method to provide Retry functionality for any asynchronous TASK
        /// </summary>
        /// <param name="task">The extension target type</param>
        /// <param name="retryOptions">The options for the retry algorithm</param>
        /// <returns>a task responsible for the retry operation</returns>
        public static async Task Retry(this Task task, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();

            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        Trace.TraceInformation($"Retrying attempt {currentRetry}");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    await task;
                    break;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        Trace.TraceError($"Operation Exception see the inner exception for the details ----> " +
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