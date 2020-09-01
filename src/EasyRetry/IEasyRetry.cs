using System;
using System.Threading.Tasks;

namespace EasyRetry
{
    public interface IEasyRetry
    {
        public Task<T> Retry<T>(Func<Task<T>> func, RetryOptions retryOptions = null);
        public Task Retry(Func<Task> func, RetryOptions retryOptions = null);
        public void Retry(Action action, RetryOptions retryOptions = null);
    }
}