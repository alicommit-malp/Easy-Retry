using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryExceptionDataTests
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
        public void Keys_HaveStableValues()
        {
            Assert.That(RetryDataKeys.Attempts, Is.EqualTo("EasyRetry.Attempts"));
            Assert.That(RetryDataKeys.Exceptions, Is.EqualTo("EasyRetry.Exceptions"));
        }

        [Test]
        public void AlwaysFails_Async_DataContainsAttemptCountAndAllExceptionsInOrder()
        {
            var thrownList = new List<Exception>();
            Func<Task> act = () => _retry.Retry<int>(() =>
            {
                var ex = new InvalidOperationException("n" + thrownList.Count);
                thrownList.Add(ex);
                throw ex;
            }, Fast(3));
            var final = Assert.ThrowsAsync<InvalidOperationException>(act)!;

            Assert.That(final.Data[RetryDataKeys.Attempts], Is.EqualTo(3));
            var all = final.Data[RetryDataKeys.Exceptions] as IReadOnlyList<Exception>;
            Assert.That(all, Is.Not.Null);
            Assert.That(all, Is.EqualTo(thrownList).AsCollection);
            Assert.That(all![2], Is.SameAs(final));
        }

        [Test]
        public void AlwaysFails_Sync_DataContainsAttemptCountAndAllExceptionsInOrder()
        {
            var thrownList = new List<Exception>();
            Action action = () =>
            {
                var ex = new InvalidOperationException("n" + thrownList.Count);
                thrownList.Add(ex);
                throw ex;
            };
            Action act = () => _retry.Retry(action, Fast(3));
            var final = Assert.Throws<InvalidOperationException>(act)!;

            Assert.That(final.Data[RetryDataKeys.Attempts], Is.EqualTo(3));
            var all = final.Data[RetryDataKeys.Exceptions] as IReadOnlyList<Exception>;
            Assert.That(all, Is.Not.Null);
            Assert.That(all, Is.EqualTo(thrownList).AsCollection);
        }

        [Test]
        public void StoppedEarlyByPredicate_DataReflectsSingleAttempt()
        {
            var options = Fast(5);
            options.ShouldRetry = (_, _) => false;
            Func<Task> act = () => _retry.Retry(() => throw new InvalidOperationException(), options);
            var final = Assert.ThrowsAsync<InvalidOperationException>(act)!;

            Assert.That(final.Data[RetryDataKeys.Attempts], Is.EqualTo(1));
            var all = (IReadOnlyList<Exception>)final.Data[RetryDataKeys.Exceptions]!;
            Assert.That(all, Has.Count.EqualTo(1));
            Assert.That(all[0], Is.SameAs(final));
        }
    }
}
