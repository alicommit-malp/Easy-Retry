using System;
using System.Threading;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryOptionsPresetTests
    {
        private static void AssertOtherDefaults(RetryOptions o)
        {
            Assert.That(o.DelayBeforeFirstTry, Is.EqualTo(TimeSpan.Zero));
            Assert.That(o.EnableLogging, Is.False);
            Assert.That(o.DoNotRetryOnTheseExceptionTypes, Is.Not.Null.And.Empty);
            Assert.That(o.ShouldRetry, Is.Null);
            Assert.That(o.CancellationToken, Is.EqualTo(CancellationToken.None));
            Assert.That(o.RetryOnResult, Is.Null);
            Assert.That(o.MaxTotalDuration, Is.Null);
        }

        [Test]
        public void Constant_SetsAttemptsDelayAndBackoffOnly()
        {
            var o = RetryOptions.Constant(4, TimeSpan.FromMilliseconds(30));
            Assert.That(o.Attempts, Is.EqualTo(4));
            Assert.That(o.DelayBetweenRetries, Is.EqualTo(TimeSpan.FromMilliseconds(30)));
            Assert.That(o.Backoff, Is.EqualTo(RetryBackoff.Constant));
            Assert.That(o.MaxDelay, Is.Null);
            Assert.That(o.UseJitter, Is.False);
            AssertOtherDefaults(o);
        }

        [Test]
        public void Linear_SetsAttemptsDelayAndBackoffOnly()
        {
            var o = RetryOptions.Linear(5, TimeSpan.FromMilliseconds(20));
            Assert.That(o.Attempts, Is.EqualTo(5));
            Assert.That(o.DelayBetweenRetries, Is.EqualTo(TimeSpan.FromMilliseconds(20)));
            Assert.That(o.Backoff, Is.EqualTo(RetryBackoff.Linear));
            Assert.That(o.MaxDelay, Is.Null);
            Assert.That(o.UseJitter, Is.False);
            AssertOtherDefaults(o);
        }

        [Test]
        public void Exponential_Defaults_JitterOnNoCap()
        {
            var o = RetryOptions.Exponential(6, TimeSpan.FromMilliseconds(10));
            Assert.That(o.Attempts, Is.EqualTo(6));
            Assert.That(o.DelayBetweenRetries, Is.EqualTo(TimeSpan.FromMilliseconds(10)));
            Assert.That(o.Backoff, Is.EqualTo(RetryBackoff.Exponential));
            Assert.That(o.MaxDelay, Is.Null);
            Assert.That(o.UseJitter, Is.True);
            AssertOtherDefaults(o);
        }

        [Test]
        public void Exponential_WithCapAndJitterOff()
        {
            var o = RetryOptions.Exponential(6, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1), useJitter: false);
            Assert.That(o.MaxDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(o.UseJitter, Is.False);
        }

        [Test]
        public void Presets_DoNotValidate()
        {
            Action constant = () => RetryOptions.Constant(0, TimeSpan.FromMilliseconds(-1));
            Action linear = () => RetryOptions.Linear(-1, TimeSpan.FromMilliseconds(-1));
            Action exponential = () => RetryOptions.Exponential(0, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            Assert.DoesNotThrow(constant);
            Assert.DoesNotThrow(linear);
            Assert.DoesNotThrow(exponential);
        }

        [Test]
        public void Presets_ReturnNewInstanceEachCall()
        {
            Assert.That(RetryOptions.Constant(1, TimeSpan.Zero), Is.Not.SameAs(RetryOptions.Constant(1, TimeSpan.Zero)));
        }
    }
}
