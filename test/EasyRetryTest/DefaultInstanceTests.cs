using System;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class DefaultInstanceTests
    {
        [Test]
        public void Default_IsNotNull_AndIsIEasyRetry()
        {
            Assert.That(EasyRetry.EasyRetry.Default, Is.Not.Null);
            Assert.That(EasyRetry.EasyRetry.Default, Is.InstanceOf<IEasyRetry>());
        }

        [Test]
        public void Default_ReturnsSameInstance()
        {
            Assert.That(EasyRetry.EasyRetry.Default, Is.SameAs(EasyRetry.EasyRetry.Default));
        }

        [Test]
        public async Task Default_RetriesAndReturnsResult()
        {
            var calls = 0;
            var options = new RetryOptions { Attempts = 3, DelayBetweenRetries = TimeSpan.FromMilliseconds(1) };
            var result = await EasyRetry.EasyRetry.Default.Retry(() =>
            {
                calls++;
                if (calls < 2) throw new InvalidOperationException();
                return Task.FromResult(calls);
            }, options);
            Assert.That(result, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void Default_WithLoggingEnabled_DoesNotThrow()
        {
            var options = new RetryOptions { Attempts = 2, DelayBetweenRetries = TimeSpan.FromMilliseconds(1), EnableLogging = true };
            Action action = () => throw new InvalidOperationException();
            Action act = () => EasyRetry.EasyRetry.Default.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
        }
    }
}
