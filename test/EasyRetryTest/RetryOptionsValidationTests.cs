using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryOptionsValidationTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        [Test]
        public void Defaults_MatchContract()
        {
            var o = new RetryOptions();
            Assert.That(o.DelayBetweenRetries, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(o.DelayBeforeFirstTry, Is.EqualTo(TimeSpan.Zero));
            Assert.That(o.Attempts, Is.EqualTo(2));
            Assert.That(o.EnableLogging, Is.False);
            Assert.That(o.DoNotRetryOnTheseExceptionTypes, Is.Not.Null.And.Empty);
        }

        [Test]
        public void NullDelegates_ThrowArgumentNullException()
        {
            Func<Task> actT = () => _retry.Retry<int>(null!);
            Assert.ThrowsAsync<ArgumentNullException>(actT);
            Func<Task> act = () => _retry.Retry((Func<Task>)null!);
            Assert.ThrowsAsync<ArgumentNullException>(act);
            Action actSync = () => _retry.Retry((Action)null!);
            Assert.Throws<ArgumentNullException>(actSync);
        }

        [Test]
        public void Logger_Null_ThrowsArgumentNullException()
        {
            Action act = () => new EasyRetry.EasyRetry(null!);
            Assert.Throws<ArgumentNullException>(act);
        }

        private static IEnumerable<TestCaseData> InvalidOptions()
        {
            yield return new TestCaseData(new RetryOptions { Attempts = 0 }, typeof(ArgumentOutOfRangeException)).SetName("{m}(Attempts=0)");
            yield return new TestCaseData(new RetryOptions { Attempts = -1 }, typeof(ArgumentOutOfRangeException)).SetName("{m}(Attempts=-1)");
            yield return new TestCaseData(new RetryOptions { DelayBetweenRetries = TimeSpan.FromMilliseconds(-5) }, typeof(ArgumentOutOfRangeException)).SetName("{m}(NegativeDelayBetweenRetries)");
            yield return new TestCaseData(new RetryOptions { DelayBetweenRetries = Timeout.InfiniteTimeSpan }, typeof(ArgumentOutOfRangeException)).SetName("{m}(InfiniteDelayBetweenRetries)");
            yield return new TestCaseData(new RetryOptions { DelayBeforeFirstTry = TimeSpan.FromMilliseconds(-5) }, typeof(ArgumentOutOfRangeException)).SetName("{m}(NegativeDelayBeforeFirstTry)");
            yield return new TestCaseData(new RetryOptions { DelayBeforeFirstTry = Timeout.InfiniteTimeSpan }, typeof(ArgumentOutOfRangeException)).SetName("{m}(InfiniteDelayBeforeFirstTry)");
            yield return new TestCaseData(new RetryOptions { MaxDelay = TimeSpan.FromMilliseconds(-5) }, typeof(ArgumentOutOfRangeException)).SetName("{m}(NegativeMaxDelay)");
            yield return new TestCaseData(new RetryOptions { MaxDelay = Timeout.InfiniteTimeSpan }, typeof(ArgumentOutOfRangeException)).SetName("{m}(InfiniteMaxDelay)");
            yield return new TestCaseData(new RetryOptions { DoNotRetryOnTheseExceptionTypes = null! }, typeof(ArgumentException)).SetName("{m}(NullDoNotRetryList)");
        }

        [TestCaseSource(nameof(InvalidOptions))]
        public void InvalidOptions_FuncT_ThrowsBeforeInvoking(RetryOptions options, Type expected)
        {
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() => { calls++; return Task.FromResult(1); }, options);
            Assert.ThrowsAsync(expected, act);
            Assert.That(calls, Is.EqualTo(0));
        }

        [TestCaseSource(nameof(InvalidOptions))]
        public void InvalidOptions_Func_ThrowsBeforeInvoking(RetryOptions options, Type expected)
        {
            var calls = 0;
            Func<Task> act = () => _retry.Retry(() => { calls++; return Task.CompletedTask; }, options);
            Assert.ThrowsAsync(expected, act);
            Assert.That(calls, Is.EqualTo(0));
        }

        [TestCaseSource(nameof(InvalidOptions))]
        public void InvalidOptions_Action_ThrowsBeforeInvoking(RetryOptions options, Type expected)
        {
            var calls = 0;
            Action act = () => _retry.Retry(() => { calls++; }, options);
            Assert.Throws(expected, act);
            Assert.That(calls, Is.EqualTo(0));
        }
    }
}
