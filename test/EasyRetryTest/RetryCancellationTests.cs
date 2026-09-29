using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryCancellationTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions Options(CancellationToken token, TimeSpan? delay = null) => new()
        {
            Attempts = 3,
            DelayBetweenRetries = delay ?? TimeSpan.FromMilliseconds(1),
            CancellationToken = token
        };

        [Test]
        public void AlreadyCancelled_ThrowsOce_DelegateNeverInvoked_Async()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() => { calls++; return Task.FromResult(1); }, Options(cts.Token));
            Assert.ThrowsAsync<OperationCanceledException>(act);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void AlreadyCancelled_ThrowsOce_DelegateNeverInvoked_Sync()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var calls = 0;
            Action act = () => _retry.Retry(() => { calls++; }, Options(cts.Token));
            Assert.Throws<OperationCanceledException>(act);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void OceFromDelegateWhileTokenNotCancelled_IsRetried_Async()
        {
            using var cts = new CancellationTokenSource();
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() => { calls++; throw new TaskCanceledException("timeout"); }, Options(cts.Token));
            Assert.ThrowsAsync<TaskCanceledException>(act);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void OceFromDelegateWithoutToken_IsRetried_Async()
        {
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() => { calls++; throw new OperationCanceledException(); }, Options(default));
            Assert.ThrowsAsync<OperationCanceledException>(act);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void DelegateCancelsTokenAndThrowsOce_NotRetried_Async()
        {
            using var cts = new CancellationTokenSource();
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() =>
            {
                calls++;
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }, Options(cts.Token));
            Assert.ThrowsAsync<OperationCanceledException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CancelledDuringDelay_ThrowsOcePromptly_Async()
        {
            using var cts = new CancellationTokenSource();
            var calls = 0;
            cts.CancelAfter(20);
            var sw = Stopwatch.StartNew();
            Func<Task> act = () => _retry.Retry(() => { calls++; throw new InvalidOperationException(); },
                Options(cts.Token, TimeSpan.FromSeconds(5)));
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), act);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CancelledDuringDelay_ThrowsOcePromptly_Sync()
        {
            using var cts = new CancellationTokenSource();
            var calls = 0;
            cts.CancelAfter(20);
            var sw = Stopwatch.StartNew();
            Action action = () => { calls++; throw new InvalidOperationException(); };
            Action act = () => _retry.Retry(action, Options(cts.Token, TimeSpan.FromSeconds(5)));
            Assert.Throws<OperationCanceledException>(act);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
