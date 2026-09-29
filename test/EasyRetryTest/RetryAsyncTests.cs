using System;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryAsyncTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions Fast(int attempts) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(1)
        };

        [Test]
        public async Task FuncT_SucceedsOnFirstTry_InvokedOnce()
        {
            var calls = 0;
            var result = await _retry.Retry(() => { calls++; return Task.FromResult(42); }, Fast(3));
            Assert.That(result, Is.EqualTo(42));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task Func_SucceedsOnFirstTry_InvokedOnce()
        {
            var calls = 0;
            await _retry.Retry(() => { calls++; return Task.CompletedTask; }, Fast(3));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task FuncT_FailsOnceThenSucceeds_InvokedTwice()
        {
            var calls = 0;
            var result = await _retry.Retry(() =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("first");
                return Task.FromResult("ok");
            }, Fast(3));
            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public async Task Func_FailsOnceThenSucceeds_InvokedTwice()
        {
            var calls = 0;
            await _retry.Retry(() =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("first");
                return Task.CompletedTask;
            }, Fast(3));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void FuncT_AlwaysFails_InvokedAttemptsTimes_OriginalExceptionPropagates()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            Func<Task> act = () => _retry.Retry<int>(() =>
            {
                calls++;
                last = new InvalidOperationException("boom " + calls);
                throw last;
            }, Fast(3));
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(ReferenceEquals(thrown, last), Is.True);
        }

        [Test]
        public void Func_AlwaysFails_InvokedAttemptsTimes_OriginalExceptionPropagates()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            Func<Task> act = () => _retry.Retry(() =>
            {
                calls++;
                last = new InvalidOperationException("boom " + calls);
                throw last;
            }, Fast(3));
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(ReferenceEquals(thrown, last), Is.True);
        }

        [Test]
        public void FuncT_AlwaysFailsAsync_OriginalExceptionPropagates()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            Func<Task> act = () => _retry.Retry<int>(async () =>
            {
                calls++;
                await Task.Yield();
                last = new InvalidOperationException("boom " + calls);
                throw last;
            }, Fast(3));
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(ReferenceEquals(thrown, last), Is.True);
        }
    }
}
