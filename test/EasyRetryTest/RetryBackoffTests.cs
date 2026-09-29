using System;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryBackoffTests
    {
        private static readonly TimeSpan Base = TimeSpan.FromMilliseconds(100);

        private static RetryOptions Options(RetryBackoff backoff, TimeSpan? maxDelay = null, bool jitter = false) => new()
        {
            DelayBetweenRetries = Base,
            Backoff = backoff,
            MaxDelay = maxDelay,
            UseJitter = jitter
        };

        [Test]
        public void Defaults_ConstantBackoff_NoMaxDelay_NoJitter()
        {
            var o = new RetryOptions();
            Assert.That(o.Backoff, Is.EqualTo(RetryBackoff.Constant));
            Assert.That(o.MaxDelay, Is.Null);
            Assert.That(o.UseJitter, Is.False);
            Assert.That(o.CancellationToken, Is.EqualTo(default(System.Threading.CancellationToken)));
            Assert.That(o.ShouldRetry, Is.Null);
        }

        [TestCase(RetryBackoff.Constant, 1, 100)]
        [TestCase(RetryBackoff.Constant, 2, 100)]
        [TestCase(RetryBackoff.Constant, 3, 100)]
        [TestCase(RetryBackoff.Linear, 1, 100)]
        [TestCase(RetryBackoff.Linear, 2, 200)]
        [TestCase(RetryBackoff.Linear, 3, 300)]
        [TestCase(RetryBackoff.Exponential, 1, 100)]
        [TestCase(RetryBackoff.Exponential, 2, 200)]
        [TestCase(RetryBackoff.Exponential, 3, 400)]
        public void GetDelay_ComputesExpectedMilliseconds(RetryBackoff backoff, int failedAttempt, int expectedMs)
        {
            var delay = EasyRetry.EasyRetry.GetDelay(failedAttempt, Options(backoff));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(expectedMs)));
        }

        [Test]
        public void GetDelay_MaxDelay_CapsExponential()
        {
            var delay = EasyRetry.EasyRetry.GetDelay(3, Options(RetryBackoff.Exponential, TimeSpan.FromMilliseconds(250)));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
        }

        [Test]
        public void GetDelay_MaxDelay_CapsLinear()
        {
            var delay = EasyRetry.EasyRetry.GetDelay(3, Options(RetryBackoff.Linear, TimeSpan.FromMilliseconds(250)));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
        }

        [Test]
        public void GetDelay_Exponential_LargeAttempt_WithMaxDelay_IsCapped()
        {
            var delay = EasyRetry.EasyRetry.GetDelay(100, Options(RetryBackoff.Exponential, TimeSpan.FromSeconds(30)));
            Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void GetDelay_Exponential_LargeAttempt_WithoutMaxDelay_DoesNotOverflow()
        {
            var delay = EasyRetry.EasyRetry.GetDelay(100, Options(RetryBackoff.Exponential));
            Assert.That(delay, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(delay, Is.LessThanOrEqualTo(TimeSpan.MaxValue));
        }

        [Test]
        public void GetDelay_Exponential_LargeAttempt_WithoutMaxDelay_IsMonotonic()
        {
            var a = EasyRetry.EasyRetry.GetDelay(60, Options(RetryBackoff.Exponential));
            var b = EasyRetry.EasyRetry.GetDelay(100, Options(RetryBackoff.Exponential));
            Assert.That(b, Is.GreaterThanOrEqualTo(a));
        }

        [TestCase(RetryBackoff.Constant)]
        [TestCase(RetryBackoff.Linear)]
        [TestCase(RetryBackoff.Exponential)]
        public void GetDelay_WithJitter_StaysWithin20Percent(RetryBackoff backoff)
        {
            var plain = EasyRetry.EasyRetry.GetDelay(3, Options(backoff));
            for (var i = 0; i < 200; i++)
            {
                var jittered = EasyRetry.EasyRetry.GetDelay(3, Options(backoff, jitter: true));
                Assert.That(jittered.TotalMilliseconds, Is.InRange(plain.TotalMilliseconds * 0.8, plain.TotalMilliseconds * 1.2));
            }
        }

        [Test]
        public void GetDelay_WithJitter_RespectsMaxDelayAndNeverNegative()
        {
            var max = TimeSpan.FromMilliseconds(250);
            for (var i = 0; i < 200; i++)
            {
                var jittered = EasyRetry.EasyRetry.GetDelay(3, Options(RetryBackoff.Exponential, max, jitter: true));
                Assert.That(jittered, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
                Assert.That(jittered, Is.LessThanOrEqualTo(max));
            }
        }

        [Test]
        public void GetDelay_ZeroBase_WithJitter_IsZero()
        {
            var options = Options(RetryBackoff.Exponential, jitter: true);
            options.DelayBetweenRetries = TimeSpan.Zero;
            Assert.That(EasyRetry.EasyRetry.GetDelay(5, options), Is.EqualTo(TimeSpan.Zero));
        }
    }
}
