using System;
using System.Threading.Tasks;
using EasyRetry;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryLoggingTests
    {
        private static RetryOptions Fast(int attempts, bool logging) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
            EnableLogging = logging
        };

        [Test]
        public void LoggingDisabledByDefault_NoEntriesEvenOnFailure()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            Func<Task> act = () => retry.Retry(() => throw new InvalidOperationException(), Fast(3, logging: false));
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(logger.Entries, Is.Empty);
        }

        [Test]
        public async Task LoggingEnabled_FailTwiceThenSucceed_TwoWarningsWithException()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            var calls = 0;
            await retry.Retry(() =>
            {
                calls++;
                if (calls < 3) throw new InvalidOperationException("fail " + calls);
                return Task.CompletedTask;
            }, Fast(3, logging: true));

            Assert.That(logger.Entries, Has.Count.EqualTo(2));
            foreach (var entry in logger.Entries)
            {
                Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
                Assert.That(entry.Exception, Is.InstanceOf<InvalidOperationException>());
            }
            Assert.That(logger.Entries[0].Message, Does.Contain("Attempt 1 of 3"));
            Assert.That(logger.Entries[1].Message, Does.Contain("Attempt 2 of 3"));
        }

        [Test]
        public void LoggingEnabled_AlwaysFails_OneWarningThenOneError()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            Func<Task> act = () => retry.Retry<int>(() => throw new InvalidOperationException("boom"), Fast(2, logging: true));
            var final = Assert.ThrowsAsync<InvalidOperationException>(act);

            Assert.That(logger.Entries, Has.Count.EqualTo(2));
            Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries[0].Exception, Is.Not.Null);
            Assert.That(logger.Entries[1].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries[1].Exception, Is.SameAs(final));
            Assert.That(logger.Entries[1].Message, Does.Contain("Attempt 2 of 2"));
        }

        [Test]
        public void LoggingEnabled_Sync_AlwaysFails_OneWarningThenOneError()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            Action action = () => throw new InvalidOperationException("boom");
            Action act = () => retry.Retry(action, Fast(2, logging: true));
            var final = Assert.Throws<InvalidOperationException>(act);

            Assert.That(logger.Entries, Has.Count.EqualTo(2));
            Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(logger.Entries[0].Exception, Is.Not.Null);
            Assert.That(logger.Entries[1].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries[1].Exception, Is.SameAs(final));
        }

        [Test]
        public void ParameterlessConstructor_WithLoggingEnabled_DoesNotThrow()
        {
            var retry = new EasyRetry.EasyRetry();
            Func<Task> act = () => retry.Retry(() => throw new InvalidOperationException(), Fast(2, logging: true));
            Assert.ThrowsAsync<InvalidOperationException>(act);
        }
    }
}
