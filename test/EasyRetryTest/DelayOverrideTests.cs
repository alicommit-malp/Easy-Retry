using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class DelayOverrideTests
    {
        private static readonly TimeSpan Base = TimeSpan.FromMilliseconds(100);

        private static RetryOptions Options(Func<Exception?, int, TimeSpan?>? overrideDelay,
            TimeSpan? maxDelay = null, bool jitter = false) => new()
        {
            DelayBetweenRetries = Base,
            Backoff = RetryBackoff.Exponential,
            MaxDelay = maxDelay,
            UseJitter = jitter,
            DelayOverride = overrideDelay
        };

        [Test]
        public void Default_IsNull()
        {
            Assert.That(new RetryOptions().DelayOverride, Is.Null);
        }

        [Test]
        public void ResolveDelay_OverrideReplacesComputedBackoff()
        {
            var delay = EasyRetry.EasyRetry.ResolveDelay(null, 3, Options((_, _) => TimeSpan.FromMilliseconds(7)));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(7)));
        }

        [Test]
        public void ResolveDelay_MaxDelayStillCapsOverride()
        {
            var delay = EasyRetry.EasyRetry.ResolveDelay(null, 1,
                Options((_, _) => TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(20)));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(20)));
        }

        [Test]
        public void ResolveDelay_NullOverride_EqualsGetDelay()
        {
            var options = Options((_, _) => null);
            Assert.That(EasyRetry.EasyRetry.ResolveDelay(null, 3, options),
                Is.EqualTo(EasyRetry.EasyRetry.GetDelay(3, options)));
        }

        [Test]
        public void ResolveDelay_NoOverrideDelegate_EqualsGetDelay()
        {
            var options = Options(null);
            Assert.That(EasyRetry.EasyRetry.ResolveDelay(null, 3, options),
                Is.EqualTo(EasyRetry.EasyRetry.GetDelay(3, options)));
        }

        [Test]
        public void ResolveDelay_JitterNotAppliedToOverride()
        {
            var options = Options((_, _) => TimeSpan.FromMilliseconds(50), jitter: true);
            for (var i = 0; i < 50; i++)
            {
                Assert.That(EasyRetry.EasyRetry.ResolveDelay(null, 2, options), Is.EqualTo(TimeSpan.FromMilliseconds(50)));
            }
        }

        [Test]
        public void ResolveDelay_NegativeOverride_ThrowsArgumentOutOfRange()
        {
            var options = Options((_, _) => TimeSpan.FromMilliseconds(-1));
            Action act = () => EasyRetry.EasyRetry.ResolveDelay(null, 1, options);
            Assert.Throws<ArgumentOutOfRangeException>(act);
        }

        [Test]
        public void ResolveDelay_PassesExceptionAndAttemptToOverride()
        {
            Exception? seenEx = null;
            var seenAttempt = 0;
            var ex = new InvalidOperationException();
            var options = Options((e, a) => { seenEx = e; seenAttempt = a; return null; });
            EasyRetry.EasyRetry.ResolveDelay(ex, 4, options);
            Assert.That(seenEx, Is.SameAs(ex));
            Assert.That(seenAttempt, Is.EqualTo(4));
        }

        [Test]
        public void EndToEnd_Async_OverrideCalledPerRetryWithFailedAttempts()
        {
            var retry = new EasyRetry.EasyRetry();
            var seen = new List<int>();
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromSeconds(10),
                DelayOverride = (ex, attempt) =>
                {
                    Assert.That(ex, Is.InstanceOf<InvalidOperationException>());
                    seen.Add(attempt);
                    return TimeSpan.FromMilliseconds(1);
                }
            };
            Func<Task> act = () => retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void EndToEnd_Sync_OverrideCalledPerRetryWithFailedAttempts()
        {
            var retry = new EasyRetry.EasyRetry();
            var seen = new List<int>();
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromSeconds(10),
                DelayOverride = (_, attempt) => { seen.Add(attempt); return TimeSpan.FromMilliseconds(1); }
            };
            Action action = () => throw new InvalidOperationException();
            Action act = () => retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void EndToEnd_Async_NegativeOverride_PropagatesImmediately()
        {
            var retry = new EasyRetry.EasyRetry();
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                DelayOverride = (_, _) => TimeSpan.FromMilliseconds(-5)
            };
            Func<Task> act = () => retry.Retry<int>(() => { calls++; throw new InvalidOperationException(); }, options);
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task EndToEnd_Async_ResultRetry_OverrideReceivesNullException()
        {
            var retry = new EasyRetry.EasyRetry();
            var seen = new List<Exception?>();
            var options = new RetryOptions
            {
                Attempts = 2,
                DelayBetweenRetries = TimeSpan.FromSeconds(10),
                RetryOnResult = r => (int)r! < 2,
                DelayOverride = (ex, _) => { seen.Add(ex); return TimeSpan.FromMilliseconds(1); }
            };
            var calls = 0;
            await retry.Retry(() => Task.FromResult(++calls), options);
            Assert.That(seen, Is.EqualTo(new Exception?[] { null }));
        }

        [Test]
        public void EndToEnd_OnRetryReceivesOverriddenDelay()
        {
            var retry = new EasyRetry.EasyRetry();
            var seen = new List<TimeSpan>();
            var options = new RetryOptions
            {
                Attempts = 2,
                DelayBetweenRetries = TimeSpan.FromSeconds(10),
                DelayOverride = (_, _) => TimeSpan.FromMilliseconds(3),
                OnRetry = (_, _, delay) => seen.Add(delay)
            };
            Func<Task> act = () => retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { TimeSpan.FromMilliseconds(3) }));
        }
    }
}
