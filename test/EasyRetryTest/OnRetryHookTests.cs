using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class OnRetryHookTests
    {
        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(1);

        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions Fast(int attempts) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = Delay
        };

        [Test]
        public void Default_IsNull()
        {
            Assert.That(new RetryOptions().OnRetry, Is.Null);
        }

        [Test]
        public void Async_AlwaysFails_HookCalledPerRetry_NotOnGiveUp()
        {
            var seen = new List<(Exception? Ex, int Attempt, TimeSpan Delay)>();
            var options = Fast(3);
            options.OnRetry = (ex, attempt, delay) => seen.Add((ex, attempt, delay));
            Func<Task> act = () => _retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);

            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen.ConvertAll(s => s.Attempt), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(seen.ConvertAll(s => s.Ex), Has.All.InstanceOf<InvalidOperationException>());
            Assert.That(seen.ConvertAll(s => s.Delay), Has.All.EqualTo(Delay));
        }

        [Test]
        public void Sync_AlwaysFails_HookCalledPerRetry_NotOnGiveUp()
        {
            var seen = new List<(Exception? Ex, int Attempt, TimeSpan Delay)>();
            var options = Fast(3);
            options.OnRetry = (ex, attempt, delay) => seen.Add((ex, attempt, delay));
            Action action = () => throw new InvalidOperationException();
            Action act = () => _retry.Retry(action, options);
            Assert.Throws<InvalidOperationException>(act);

            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen.ConvertAll(s => s.Attempt), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(seen.ConvertAll(s => s.Ex), Has.All.InstanceOf<InvalidOperationException>());
            Assert.That(seen.ConvertAll(s => s.Delay), Has.All.EqualTo(Delay));
        }

        [Test]
        public void Async_HookReceivesTheExceptionOfTheFailedAttempt()
        {
            var thrown = new List<Exception>();
            var seen = new List<Exception?>();
            var options = Fast(3);
            options.OnRetry = (ex, _, _) => seen.Add(ex);
            Func<Task> act = () => _retry.Retry<int>(() =>
            {
                var ex = new InvalidOperationException("n" + thrown.Count);
                thrown.Add(ex);
                throw ex;
            }, options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen[0], Is.SameAs(thrown[0]));
            Assert.That(seen[1], Is.SameAs(thrown[1]));
        }

        [Test]
        public async Task ResultBasedRetry_HookReceivesNullException()
        {
            var seen = new List<(Exception? Ex, int Attempt)>();
            var options = Fast(3);
            options.RetryOnResult = r => (int)r! < 2;
            options.OnRetry = (ex, attempt, _) => seen.Add((ex, attempt));
            var calls = 0;
            var result = await _retry.Retry(() => Task.FromResult(++calls), options);

            Assert.That(result, Is.EqualTo(2));
            Assert.That(seen, Has.Count.EqualTo(1));
            Assert.That(seen[0].Ex, Is.Null);
            Assert.That(seen[0].Attempt, Is.EqualTo(1));
        }

        [Test]
        public void Async_HookThrows_PropagatesImmediately_NoFurtherAttempts()
        {
            var calls = 0;
            var options = Fast(3);
            options.OnRetry = (_, _, _) => throw new ApplicationException("hook");
            Func<Task> act = () => _retry.Retry<int>(() => { calls++; throw new InvalidOperationException(); }, options);
            var thrown = Assert.ThrowsAsync<ApplicationException>(act);
            Assert.That(thrown!.Message, Is.EqualTo("hook"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Sync_HookThrows_PropagatesImmediately_NoFurtherAttempts()
        {
            var calls = 0;
            var options = Fast(3);
            options.OnRetry = (_, _, _) => throw new ApplicationException("hook");
            Action action = () => { calls++; throw new InvalidOperationException(); };
            Action act = () => _retry.Retry(action, options);
            var thrown = Assert.Throws<ApplicationException>(act);
            Assert.That(thrown!.Message, Is.EqualTo("hook"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Async_HookThrowsOnResultRetry_PropagatesImmediately()
        {
            var calls = 0;
            var options = Fast(3);
            options.RetryOnResult = _ => true;
            options.OnRetry = (_, _, _) => throw new ApplicationException("hook");
            Func<Task> act = () => _retry.Retry(() => Task.FromResult(++calls), options);
            Assert.ThrowsAsync<ApplicationException>(act);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Hook_IsInvokedWithoutLoggingEnabled_AfterLogEntry()
        {
            var logger = new RecordingLogger();
            var retry = new EasyRetry.EasyRetry(logger);
            var logCountAtHook = new List<int>();
            var options = Fast(2);
            options.EnableLogging = true;
            options.OnRetry = (_, _, _) => logCountAtHook.Add(logger.Entries.Count);
            Func<Task> act = () => retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(logCountAtHook, Is.EqualTo(new[] { 1 }), "hook runs after the retry warning is logged");
        }

        [Test]
        public void Hook_NotCalled_WhenPredicateStopsRetrying()
        {
            var hookCalls = 0;
            var options = Fast(3);
            options.ShouldRetry = (_, _) => false;
            options.OnRetry = (_, _, _) => hookCalls++;
            Func<Task> act = () => _retry.Retry<int>(() => throw new InvalidOperationException(), options);
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(hookCalls, Is.EqualTo(0));
        }
    }
}
