using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EasyRetry
{
    public static class EasyRetryTaskExtension
    {
        public static async Task Retry(this Task task, RetryOptions retryOptions = null)
        {
            retryOptions ??= new RetryOptions();

            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    await task;
                    break;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Operation Exception see the inner exception for the details ----> " +
                                     $"Message: {ex.Message} " +
                                     $"Stack: {ex.StackTrace} ");
                    currentRetry++;
                    if (currentRetry > retryOptions.Attempts)
                    {
                        throw;
                    }
                }

                await Task.Delay(retryOptions.DelayBetweenRetries);
            }
        }
    }
}