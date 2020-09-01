using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EasyRetry;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class Tests
    {
        private static readonly ILogger<EasyRetry.EasyRetry> Logger = TestLogger.Create<EasyRetry.EasyRetry>();
        private IEasyRetry _easyRetry;

        [SetUp]
        public void Setup()
        {
            _easyRetry = new EasyRetry.EasyRetry(Logger);
        }

        [Test]
        public async Task Test()
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
                {
                    var result = await _easyRetry.Retry(async () =>
                    {
                        var response =
                            await new HttpClient().GetAsync(
                                "http://aaaaaaaaaasdhakdkaczxnkjhasdihaoajlsljasjdjaslkjasaa.com/");
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }, new RetryOptions() {Attempts = 3, EnableLogging = true});
                    Logger.LogInformation(result);
                }
            );
        }

        private async Task Task_NetworkBound_With_Await()
        {
            await new HttpClient().GetStringAsync("https://dotnetfoundation2222222hh2h222.org");
        }

        [Test]
        public async Task TestAsyncFunctionCallWithAwait()
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await _easyRetry.Retry(async () => await Task_NetworkBound_With_Await(), new RetryOptions()
                {
                    EnableLogging = true,
                    Attempts = 3
                });
            });
        }

        private Task Task_NetworkBound()
        {
            return new HttpClient().GetStringAsync("https://dotnetfoundation2222222hh2h222.org");
        }

        [Test]
        public async Task TestAsyncFunctionCall()
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await new EasyRetry.EasyRetry().Retry(async () => await Task_NetworkBound());
            });
        }

        [Test]
        public async Task TestAsyncFunctionCallFullOptions()
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await _easyRetry.Retry(async () => await Task_NetworkBound()
                    , new RetryOptions()
                    {
                        Attempts = 3,
                        DelayBetweenRetries = TimeSpan.FromSeconds(3),
                        DelayBeforeFirstTry = TimeSpan.FromSeconds(2),
                        EnableLogging = true,
                        DoNotRetryOnTheseExceptionTypes = new List<Type>()
                        {
                            typeof(NullReferenceException)
                        }
                    });
            });
        }

        [Test]
        public async Task TestFailThenSucceed()
        {
            var iteration = 1;
            _easyRetry.Retry(() =>
            {
                Logger.LogInformation($"{nameof(iteration)}:{iteration}");
                if (iteration++ == 1)
                {
                    throw new Exception();
                }
            }, new RetryOptions() {EnableLogging = true});
        }

        [Test]
        public void TestWithAllOptions()
        {
            Assert.ThrowsAsync<DivideByZeroException>(code: async () =>
            {
                _easyRetry.Retry(() =>
                    {
                        {
                            var zero = 0;
                            var result = 1 / zero;
                        }
                    },
                    new RetryOptions()
                    {
                        Attempts = 3,
                        DelayBetweenRetries = TimeSpan.FromSeconds(3),
                        DelayBeforeFirstTry = TimeSpan.FromSeconds(2),
                        EnableLogging = true,
                        DoNotRetryOnTheseExceptionTypes = new List<Type>()
                        {
                            typeof(NullReferenceException)
                        }
                    });
            });
        }
    }
}