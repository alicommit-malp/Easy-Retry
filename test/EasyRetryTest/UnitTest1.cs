using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyRetry;
using NUnit.Framework;

namespace EasyRetryTest
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task Test1()
        {
            var a = 0;
            await Task.Run(() =>
            {
                var i = 1 / a;
            }).Retry(new RetryOptions()
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
        }
    }
}