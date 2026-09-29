using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EasyRetry;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class MaxTotalDurationTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions Budgeted(int attempts, TimeSpan? budget) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(50),
            MaxTotalDuration = budget
        };

        [Test]
        public void Default_IsNull()
        {
            Assert.That(new RetryOptions().MaxTotalDuration, Is.Null);
        }

        private static IEnumerable<TestCaseData> InvalidBudgets()
        {
            yield return new TestCaseData(TimeSpan.FromMilliseconds(-5)).SetName("{m}(Negative)");
            yield return new TestCaseData(Timeout.InfiniteTimeSpan).SetName("{m}(Infinite)");
        }

        [TestCaseSource(nameof(InvalidBudgets))]
        public void NegativeBudget_ThrowsBeforeInvoking_AllOverloads(TimeSpan budget)
        {
            var calls = 0;
            Func<Task> actT = () => _retry.Retry(() => { calls++; return Task.FromResult(1); }, Budgeted(3, budget));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(actT);
            Func<Task> act = () => _retry.Retry(() => { calls++; return Task.CompletedTask; }, Budgeted(3, budget));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(act);
            Action actSync = () => _retry.Retry(() => { calls++; }, Budgeted(3, budget));
            Assert.Throws<ArgumentOutOfRangeException>(actSync);
            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Async_BudgetExhausted_StopsEarly_RethrowsOriginal()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            var sw = Stopwatch.StartNew();
            Func<Task> act = () => _retry.Retry<int>(() =>
            {
                calls++;
                last = new InvalidOperationException("boom " + calls);
                throw last;
            }, Budgeted(10, TimeSpan.FromMilliseconds(120)));
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(act);
            sw.Stop();
            Assert.That(calls, Is.InRange(2, 3));
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(400));
            Assert.That(thrown, Is.SameAs(last));
            Assert.That(thrown!.Data[RetryDataKeys.Attempts], Is.EqualTo(calls));
        }

        [Test]
        public void Sync_BudgetExhausted_StopsEarly_RethrowsOriginal()
        {
            var calls = 0;
            InvalidOperationException? last = null;
            Action action = () =>
            {
                calls++;
                last = new InvalidOperationException("boom " + calls);
                throw last;
            };
            var sw = Stopwatch.StartNew();
            Action act = () => _retry.Retry(action, Budgeted(10, TimeSpan.FromMilliseconds(120)));
            var thrown = Assert.Throws<InvalidOperationException>(act);
            sw.Stop();
            Assert.That(calls, Is.InRange(2, 3));
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(400));
            Assert.That(thrown, Is.SameAs(last));
            Assert.That(thrown!.Data[RetryDataKeys.Attempts], Is.EqualTo(calls));
        }

        [Test]
        public void Async_NoBudget_UsesAllAttempts()
        {
            var calls = 0;
            Func<Task> act = () => _retry.Retry<int>(() => { calls++; throw new InvalidOperationException(); },
                Budgeted(3, null));
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void Sync_NoBudget_UsesAllAttempts()
        {
            var calls = 0;
            Action action = () => { calls++; throw new InvalidOperationException(); };
            Action act = () => _retry.Retry(action, Budgeted(3, null));
            Assert.Throws<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void Async_BudgetExhausted_LogsErrorGiveUp()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            var options = Budgeted(10, TimeSpan.FromMilliseconds(120));
            options.EnableLogging = true;
            Func<Task> act = () => retry.Retry<int>(() => throw new InvalidOperationException(), options);
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(logger.Entries[^1].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries[^1].Exception, Is.SameAs(thrown));
            Assert.That(logger.Entries[^1].Message, Does.Contain("giving up"));
        }

        [Test]
        public async Task Async_BadResult_BudgetExhausted_ReturnsLastResult()
        {
            var calls = 0;
            var options = Budgeted(10, TimeSpan.FromMilliseconds(120));
            options.RetryOnResult = _ => true;
            var sw = Stopwatch.StartNew();
            var result = await _retry.Retry(() => Task.FromResult(++calls), options);
            sw.Stop();
            Assert.That(calls, Is.InRange(2, 3));
            Assert.That(result, Is.EqualTo(calls));
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(400));
        }

        [Test]
        public void DelayBeforeFirstTry_CountsAgainstBudget_Async()
        {
            var calls = 0;
            var options = Budgeted(10, TimeSpan.FromMilliseconds(120));
            options.DelayBeforeFirstTry = TimeSpan.FromMilliseconds(100);
            Func<Task> act = () => _retry.Retry<int>(() => { calls++; throw new InvalidOperationException(); }, options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
