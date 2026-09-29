using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class RetryPolicyTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        [Test]
        public void DelayBeforeFirstTry_AppliedOnceBeforeFirstAttemptOnly_Async()
        {
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBeforeFirstTry = TimeSpan.FromMilliseconds(150),
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1)
            };
            var sw = Stopwatch.StartNew();
            Func<Task> act = () => _retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(140).And.LessThan(300));
        }

        [Test]
        public void DelayBeforeFirstTry_AppliedOnceBeforeFirstAttemptOnly_Sync()
        {
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBeforeFirstTry = TimeSpan.FromMilliseconds(150),
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1)
            };
            Action action = () => throw new InvalidOperationException();
            var sw = Stopwatch.StartNew();
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(140).And.LessThan(300));
        }

        [Test]
        public void DoNotRetryList_MatchesDerivedTypes_Async()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                DoNotRetryOnTheseExceptionTypes = new List<Type> { typeof(IOException) }
            };
            Func<Task> act = () => _retry.Retry(() => { calls++; throw new FileNotFoundException(); }, options);
            Assert.ThrowsAsync<FileNotFoundException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DoNotRetryList_MatchesDerivedTypes_Sync()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                DoNotRetryOnTheseExceptionTypes = new List<Type> { typeof(IOException) }
            };
            Action action = () => { calls++; throw new FileNotFoundException(); };
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<FileNotFoundException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }
        [Test]
        public void ShouldRetry_ReturningFalseOnFirstFailure_StopsRetrying_Async()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                ShouldRetry = (_, _) => false
            };
            Func<Task> act = () => _retry.Retry(() => { calls++; throw new InvalidOperationException(); }, options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ShouldRetry_ReceivesExceptionAndOneBasedAttempt_Async()
        {
            var seen = new List<(Exception Ex, int Attempt)>();
            var thrownList = new List<Exception>();
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                ShouldRetry = (ex, attempt) => { seen.Add((ex, attempt)); return true; }
            };
            Func<Task> act = () => _retry.Retry<int>(() =>
            {
                var ex = new InvalidOperationException("n" + thrownList.Count);
                thrownList.Add(ex);
                throw ex;
            }, options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen.Count, Is.EqualTo(2), "predicate is not consulted after the final attempt");
            Assert.That(seen.ConvertAll(s => s.Attempt), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(seen[0].Ex, Is.SameAs(thrownList[0]));
            Assert.That(seen[1].Ex, Is.SameAs(thrownList[1]));
        }

        [Test]
        public void ShouldRetry_ReceivesExceptionAndOneBasedAttempt_Sync()
        {
            var attempts = new List<int>();
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 3,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
                ShouldRetry = (_, attempt) => { attempts.Add(attempt); return attempt < 2; }
            };
            Action action = () => { calls++; throw new InvalidOperationException(); };
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(attempts, Is.EqualTo(new[] { 1, 2 }));
        }
        [Test]
        public void DoNotRetryList_IsSnapshottedAtEntry_MutationDuringRetriesIgnored_Async()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 4,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1)
            };
            Func<Task> act = () => _retry.Retry(() =>
            {
                calls++;
                options.DoNotRetryOnTheseExceptionTypes.Add(typeof(InvalidOperationException));
                throw new InvalidOperationException();
            }, options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(4));
        }

        [Test]
        public void DoNotRetryList_IsSnapshottedAtEntry_MutationDuringRetriesIgnored_Sync()
        {
            var calls = 0;
            var options = new RetryOptions
            {
                Attempts = 4,
                DelayBetweenRetries = TimeSpan.FromMilliseconds(1)
            };
            Action action = () =>
            {
                calls++;
                options.DoNotRetryOnTheseExceptionTypes.Add(typeof(InvalidOperationException));
                throw new InvalidOperationException();
            };
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);
            Assert.That(calls, Is.EqualTo(4));
        }
    }
}
