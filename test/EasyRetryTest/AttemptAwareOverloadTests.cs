using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class AttemptAwareOverloadTests
    {
        private IEasyRetry _retry = null!;

        [SetUp]
        public void SetUp() => _retry = new EasyRetry.EasyRetry();

        private static RetryOptions Fast(int attempts, CancellationToken token = default) => new()
        {
            Attempts = attempts,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(1),
            CancellationToken = token
        };

        /// <summary>Invokes the delegate exactly once and never retries; proves the extensions only use the interface.</summary>
        private sealed class InvokeOnce : IEasyRetry
        {
            public int Calls;

            public Task<T> Retry<T>(Func<Task<T>> func, RetryOptions? retryOptions = null)
            {
                Calls++;
                return func();
            }

            public Task Retry(Func<Task> func, RetryOptions? retryOptions = null)
            {
                Calls++;
                return func();
            }

            public void Retry(Action action, RetryOptions? retryOptions = null)
            {
                Calls++;
                action();
            }
        }

        [Test]
        public void FuncT_ReceivesOneBasedAttemptNumbers()
        {
            var seen = new List<int>();
            Func<Task> act = () => _retry.Retry<int>((attempt, _) => { seen.Add(attempt); throw new InvalidOperationException(); }, Fast(3));
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Func_ReceivesOneBasedAttemptNumbers()
        {
            var seen = new List<int>();
            Func<Task> act = () => _retry.Retry((int attempt, CancellationToken _) => { seen.Add(attempt); throw new InvalidOperationException(); }, Fast(3));
            Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Action_ReceivesOneBasedAttemptNumbers()
        {
            var seen = new List<int>();
            Action<int, CancellationToken> action = (attempt, _) => { seen.Add(attempt); throw new InvalidOperationException(); };
            Action act = () => _retry.Retry(action, Fast(3));
            Assert.Throws<InvalidOperationException>(act);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task FuncT_SucceedsOnSecondAttempt_ReturnsResult()
        {
            var result = await _retry.Retry((attempt, _) =>
            {
                if (attempt < 2) throw new InvalidOperationException();
                return Task.FromResult(attempt * 10);
            }, Fast(3));
            Assert.That(result, Is.EqualTo(20));
        }

        [Test]
        public async Task AllOverloads_ReceiveTokenFromOptions()
        {
            using var cts = new CancellationTokenSource();
            var options = Fast(1, cts.Token);

            CancellationToken seenT = default;
            await _retry.Retry((_, token) => { seenT = token; return Task.FromResult(0); }, options);
            Assert.That(seenT, Is.EqualTo(cts.Token));
            Assert.That(seenT.CanBeCanceled, Is.True);

            CancellationToken seen = default;
            await _retry.Retry((int _, CancellationToken token) => { seen = token; return Task.CompletedTask; }, options);
            Assert.That(seen, Is.EqualTo(cts.Token));

            CancellationToken seenSync = default;
            _retry.Retry((int _, CancellationToken token) => { seenSync = token; }, options);
            Assert.That(seenSync, Is.EqualTo(cts.Token));
        }

        [Test]
        public async Task NullOptions_TokenIsNone_AttemptIsOne()
        {
            var (attempt, token) = await _retry.Retry((a, t) => Task.FromResult((a, t)));
            Assert.That(attempt, Is.EqualTo(1));
            Assert.That(token, Is.EqualTo(CancellationToken.None));
        }

        [Test]
        public async Task WorksAgainstAnyIEasyRetryImplementation()
        {
            var fake = new InvokeOnce();
            var seenT = 0;
            var resultT = await fake.Retry((attempt, _) => { seenT = attempt; return Task.FromResult("r"); }, Fast(3));
            var seen = 0;
            await fake.Retry((int attempt, CancellationToken _) => { seen = attempt; return Task.CompletedTask; }, Fast(3));
            var seenSync = 0;
            fake.Retry((int attempt, CancellationToken _) => { seenSync = attempt; }, Fast(3));

            Assert.That(resultT, Is.EqualTo("r"));
            Assert.That(fake.Calls, Is.EqualTo(3));
            Assert.That((seenT, seen, seenSync), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void NullArguments_ThrowArgumentNullException()
        {
            IEasyRetry nullRetry = null!;
            Action nullRetryT = () => nullRetry.Retry((int _, CancellationToken _) => Task.FromResult(1));
            Action nullRetryTask = () => nullRetry.Retry((int _, CancellationToken _) => Task.CompletedTask);
            Action nullRetrySync = () => nullRetry.Retry((int _, CancellationToken _) => { });
            Assert.Throws<ArgumentNullException>(nullRetryT);
            Assert.Throws<ArgumentNullException>(nullRetryTask);
            Assert.Throws<ArgumentNullException>(nullRetrySync);

            Action nullFuncT = () => _retry.Retry((Func<int, CancellationToken, Task<int>>)null!);
            Action nullFunc = () => _retry.Retry((Func<int, CancellationToken, Task>)null!);
            Action nullAction = () => _retry.Retry((Action<int, CancellationToken>)null!);
            Assert.Throws<ArgumentNullException>(nullFuncT);
            Assert.Throws<ArgumentNullException>(nullFunc);
            Assert.Throws<ArgumentNullException>(nullAction);
        }

        [Test]
        public async Task ZeroArgLambdas_StillBindToInterfaceMethods()
        {
            var result = await _retry.Retry(() => Task.FromResult(1), Fast(1));
            await _retry.Retry(() => Task.CompletedTask, Fast(1));
            _retry.Retry(() => { }, Fast(1));
            Assert.That(result, Is.EqualTo(1));
        }
    }
}
