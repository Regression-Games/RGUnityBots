using System;
using NUnit.Framework;
using RegressionGames.ActionManager;
using UnityEngine;

namespace Tests.Runtime
{
    public class RGActionManagerTests
    {
        [Test]
        public void TestValueRanges()
        {
            {
                RGBoolRange rng = new RGBoolRange(false, true);
                Assert.IsFalse((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 2);
                Assert.IsFalse((bool)rng[0]);
                Assert.IsTrue((bool)rng[1]);
            }
            {
                RGBoolRange rng = new RGBoolRange(true, true);
                Assert.IsTrue((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 1);
                Assert.IsTrue((bool)rng[0]);
            }
            {
                RGFloatRange rng = new RGFloatRange(-1.0f, 1.0f);
                Assert.IsTrue(Mathf.Approximately((float)rng.MinValue, -1.0f));
                Assert.IsTrue(Mathf.Approximately((float)rng.MaxValue, 1.0f));

                RGContinuousValueRange[] disc = rng.Discretize(4);
                Assert.IsTrue(Mathf.Approximately((float)disc[0].MinValue, -1.0f));
                Assert.IsTrue(Mathf.Approximately((float)disc[0].MaxValue, -0.5f));
                Assert.IsTrue(Mathf.Approximately((float)disc[3].MinValue, 0.5f));
                Assert.IsTrue(Mathf.Approximately((float)disc[3].MaxValue, 1.0f));
            }
            {
                RGVector2Range rng = new RGVector2Range(new Vector2(-1.0f, -1.0f), new Vector2(1.0f, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)rng.MidPoint).x, 0.0f));
                RGContinuousValueRange[] disc = rng.Discretize(4);
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[0].MinValue).x, -1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[0].MaxValue).x, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[2].MinValue).y, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[2].MaxValue).y, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MinValue).x, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MaxValue).x, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MinValue).y, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MaxValue).y, 1.0f));
            }
        }
    }
}