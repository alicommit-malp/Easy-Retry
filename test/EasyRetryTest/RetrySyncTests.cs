using System;
using System.Diagnostics;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetrySyncTests
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
        public void Action_SucceedsOnFirstTry_InvokedOnce()
        {
            var calls = 0;
            _retry.Retry(() => { calls++; }, Fast(3));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Action_FailsOnceThenSucceeds_InvokedTwice()
        {
            var calls = 0;
            _retry.Retry(() =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("first");
            }, Fast(3));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void Action_AlwaysFails_InvokedAttemptsTimes_OriginalExceptionPropagates()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            Action action = () =>
            {
                calls++;
                last = new InvalidOperationException("boom " + calls);
                throw last;
            };
            Action act = () => _retry.Retry(action, Fast(3));
            var thrown = Assert.Throws<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(ReferenceEquals(thrown, last), Is.True);
        }
        [Test]
        public void Action_RespectsDelayBetweenRetries()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(30)
            };
            Action action = () => { calls++; throw new InvalidOperationException(); };
            var sw = Stopwatch.StartNew();
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
            sw.Stop();
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(55));
        }
    }
}
