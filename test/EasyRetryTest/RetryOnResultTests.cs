using System;
using System.Threading.Tasks;
using EasyRetry;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryOnResultTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions RetryWhileBelowThree(int attempts) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
            RetryOnResult = r => (int)r! < 3
        };

        [Test]
        public void Default_IsNull()
        {
            Assert.That(new RetryOptions().RetryOnResult, Is.Null);
        }

        [Test]
        public async Task BadResultTwiceThenGood_InvokedThreeTimes_ReturnsGood()
        {
            var calls = 0;
            var result = await _retry.Retry(() => Task.FromResult(++calls), RetryWhileBelowThree(5));
            Assert.That(result, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public async Task AlwaysBad_InvokedAttemptsTimes_ReturnsLastResult_NoException()
        {
            var calls = 0;
            var result = await _retry.Retry(() => { calls++; return Task.FromResult(-calls); }, RetryWhileBelowThree(3));
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(result, Is.EqualTo(-3));
        }

        [Test]
        public async Task ExceptionThenBadResultThenGood_ReturnsGood()
        {
            var calls = 0;
            var result = await _retry.Retry(() =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("first");
                return Task.FromResult(calls == 2 ? 0 : 3);
            }, RetryWhileBelowThree(3));
            Assert.That(result, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public async Task GoodResultOnFirstTry_InvokedOnce()
        {
            var calls = 0;
            var result = await _retry.Retry(() => { calls++; return Task.FromResult(10); }, RetryWhileBelowThree(3));
            Assert.That(result, Is.EqualTo(10));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task FuncTask_NeverConsultsRetryOnResult()
        {
            var options = RetryWhileBelowThree(3);
            options.RetryOnResult = _ => throw new Exception("must not be called");
            var calls = 0;
            await _retry.Retry(() => { calls++; return Task.CompletedTask; }, options);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Action_NeverConsultsRetryOnResult()
        {
            var options = RetryWhileBelowThree(3);
            options.RetryOnResult = _ => throw new Exception("must not be called");
            var calls = 0;
            _retry.Retry(() => { calls++; }, options);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ShouldRetryPredicateAndDoNotRetryList_DoNotApplyToResultFailures()
        {
            var options = RetryWhileBelowThree(3);
            options.ShouldRetry = (_, _) => false;
            options.DoNotRetryOnTheseExceptionTypes.Add(typeof(Exception));
            var calls = 0;
            var result = await _retry.Retry(() => Task.FromResult(++calls), options);
            Assert.That(result, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public async Task Logging_AlwaysBad_WarningPerRetryAndWarningOnGiveUp_WithoutException()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            var options = RetryWhileBelowThree(3);
            options.EnableLogging = true;

            var result = await retry.Retry(() => Task.FromResult(0), options);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(logger.Entries, Has.Count.EqualTo(3));
            foreach (var entry in logger.Entries)
            {
                Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
                Assert.That(entry.Exception, Is.Null);
            }
            Assert.That(logger.Entries[0].Message, Does.Contain("Attempt 1 of 3").And.Contain("returned a result that requires retry; retrying in"));
            Assert.That(logger.Entries[1].Message, Does.Contain("Attempt 2 of 3").And.Contain("retrying in"));
            Assert.That(logger.Entries[2].Message, Does.Contain("Attempt 3 of 3").And.Contain("giving up and returning it"));
        }

        [Test]
        public async Task Logging_BadResultThenGood_SingleWarning()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            var options = RetryWhileBelowThree(3);
            options.EnableLogging = true;
            var calls = 0;

            var result = await retry.Retry(() => Task.FromResult(++calls == 1 ? 0 : 5), options);

            Assert.That(result, Is.EqualTo(5));
            Assert.That(logger.Entries, Has.Count.EqualTo(1));
            Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries[0].Message, Does.Contain("Attempt 1 of 3"));
        }
    }
}
