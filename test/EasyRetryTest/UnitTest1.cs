using System;
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
            var result = await _easyRetry.Retry(async () =>
            {
                var response = await new HttpClient().GetAsync("http://localhost:8080/");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }, new RetryOptions() {Attempts = 3, EnableLogging = true});

            Logger.LogInformation(result);
        }


        [Test]
        public async Task TestFailThenSucceed()
        {
            var iteration = 1;
            await _easyRetry.Retry(async () =>
            {
                Logger.LogInformation($"{nameof(iteration)}:{iteration}");
                if (iteration++ == 1)
                {
                    throw new Exception();
                }
            }, new RetryOptions() {EnableLogging = true});
        }

        // [Test]
        // public void Test1()
        // {
        //     Assert.ThrowsAsync<DivideByZeroException>(async () =>
        //     {
        //         var counter = 1;
        //         await new Task(() =>
        //         {
        //             Logger.LogInformation($"{nameof(counter)}:{counter++}");
        //             var zero = 0;
        //             var result = 1 / zero;
        //         }).Retry(
        //             new RetryOptions()
        //             {
        //                 Attempts = 3,
        //                 DelayBetweenRetries = TimeSpan.FromSeconds(3),
        //                 DelayBeforeFirstTry = TimeSpan.FromSeconds(2),
        //                 EnableLogging = true,
        //                 DoNotRetryOnTheseExceptionTypes = new List<Type>()
        //                 {
        //                     typeof(NullReferenceException)
        //                 }
        //             });
        //     });
        // }
    }
}