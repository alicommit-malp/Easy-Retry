using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EasyRetry
{
    public static class EasyRetryTaskExtension
    {
        public static async ValueTask Retry(this ValueTask task, RetryOptions retryOptions = null)
        {
            var tasks = new Queue<ValueTask>();
            tasks.Enqueue(task);
            tasks.Enqueue(task);


            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        Trace.TraceInformation($"Retrying attempt {currentRetry}");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    await tasks.Dequeue();
                    break;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        Trace.TraceError(
                            $"Operation Exception see the inner exception for the details ----> " +
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
        /// An extension method to provide Retry functionality for any asynchronous TASK
        /// </summary>
        /// <param name="task">The extension target type</param>
        /// <param name="retryOptions">The options for the retry algorithm</param>
        /// <returns>a task responsible for the retry operation</returns>
        public static async Task<T> Retry<T>(this Task<T> task, RetryOptions retryOptions = null)
        {
            
            retryOptions ??= new RetryOptions();
            var tasks = new Queue<Task<T>>();
            for (int i = 0; i < retryOptions.Attempts; i++)
            {
                tasks.Enqueue(task);
            }

            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        Trace.TraceInformation($"Retrying attempt {currentRetry}");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    var result= await tasks.Dequeue();
                    return result;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        Trace.TraceError(
                            $"Operation Exception see the inner exception for the details ----> " +
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
        /// An extension method to provide Retry functionality for any asynchronous TASK
        /// </summary>
        /// <param name="task">The extension target type</param>
        /// <param name="retryOptions">The options for the retry algorithm</param>
        /// <returns>a task responsible for the retry operation</returns>
        public static async Task Retry(this Action action, RetryOptions retryOptions = null)
        {
            var tasks = new Queue<Action>();
            tasks.Enqueue(action);
            tasks.Enqueue(action);


            retryOptions ??= new RetryOptions();
            var currentRetry = 1;

            for (;;)
            {
                try
                {
                    if (currentRetry > 1 && retryOptions.EnableLogging)
                        Trace.TraceInformation($"Retrying attempt {currentRetry}");
                    await Task.Delay(retryOptions.DelayBeforeFirstTry);

                    await new Task(tasks.Dequeue());
                    break;
                }
                catch (Exception ex)
                {
                    if (retryOptions.EnableLogging)
                        Trace.TraceError(
                            $"Operation Exception see the inner exception for the details ----> " +
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