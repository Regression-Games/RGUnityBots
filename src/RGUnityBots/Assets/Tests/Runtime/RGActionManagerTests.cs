using NUnit.Framework;
using RegressionGames.ActionManager;
using UnityEngine;

#if ENABLE_LEGACY_INPUT_MANAGER
namespace Tests.Runtime
{
    public class RGActionManagerTests
    {
        [Test]
        public void TestValueRanges()
        {
            RGBoolRange rng = new RGBoolRange(false, true);
            Debug.Assert(rng.NumValues == 2);
            Debug.Assert((bool)rng.MinValue == false);
            Debug.Assert((bool)rng.MaxValue == true);
            
        }
    }
}
#endif