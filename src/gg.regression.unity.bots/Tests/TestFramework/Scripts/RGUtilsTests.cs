using System.IO;
using NUnit.Framework;
using RegressionGames;

namespace Tests.TestFramework
{
    [TestFixture]
    public class RGUtilsTests
    {
        [Test]
        public void CalculateMD5ComputesCorrectHash()
        {
            var tmpFile = Path.GetTempFileName();
            File.WriteAllText(tmpFile, "Hello, World!");
            var hash = RGUtils.CalculateMD5(tmpFile);
            Assert.AreEqual("65a8e27d8879283831b664bd8b7f0ad4", hash);
        }
    }
}
