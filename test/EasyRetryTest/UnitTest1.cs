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
            }).Retry();
        }
    }
}