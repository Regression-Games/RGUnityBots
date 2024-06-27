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
                RGIntRange rng = new RGIntRange(-1, 1);
                Assert.AreEqual(rng.MinValue, -1);
                Assert.AreEqual(rng.MaxValue, 1);
                Assert.AreEqual(rng.NumValues, 3);
                Assert.AreEqual(rng[0], -1);
                Assert.AreEqual(rng[1], 0);
                Assert.AreEqual(rng[2], 1);
            }
            {
                RGVector2IntRange rng = new RGVector2IntRange(new Vector2Int(-2, -1), new Vector2Int(2, 1));
                RGVector2IntRange rng2 = new RGVector2IntRange(new Vector2Int(-2, -1), new Vector2Int(2, 1));
                Assert.IsTrue(rng.RangeEquals(rng2));
                Assert.AreEqual(rng.Width, 5);
                Assert.AreEqual(rng.Height, 3);
                Assert.AreEqual(rng.NumValues, 15);
                Assert.AreEqual(rng[0], new Vector2Int(-2, -1));
                Assert.AreEqual(rng[1], new Vector2Int(-1, -1));
                Assert.AreEqual(rng[2], new Vector2Int(0, -1));
                Assert.AreEqual(rng[3], new Vector2Int(1, -1));
                Assert.AreEqual(rng[4], new Vector2Int(2, -1));
                Assert.AreEqual(rng[5], new Vector2Int(-2, 0));
                Assert.AreEqual(rng[6], new Vector2Int(-1, 0));
                Assert.AreEqual(rng[7], new Vector2Int(0, 0));
                Assert.AreEqual(rng[8], new Vector2Int(1, 0));
                Assert.AreEqual(rng[9], new Vector2Int(2, 0));
                Assert.AreEqual(rng[10], new Vector2Int(-2, 1));
                Assert.AreEqual(rng[11], new Vector2Int(-1, 1));
                Assert.AreEqual(rng[12], new Vector2Int(0, 1));
                Assert.AreEqual(rng[13], new Vector2Int(1, 1));
                Assert.AreEqual(rng[14], new Vector2Int(2, 1));
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