using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.ActionManager
{
    // This class is used to define the range of acceptable values for action parameters.
    public interface IRGValueRange
    {
        public object MinValue { get; }
        public object MaxValue { get; }
        public object RandomSample();
    }
    
    // All discrete value types (bool, char, int, etc.) derived from RGDiscreteValueRange
    public abstract class RGDiscreteValueRange : IRGValueRange
    {
        public abstract object MinValue { get; }
        public abstract object MaxValue { get; }
        
        // The number of possible values in this discrete value range.
        public abstract int NumValues { get; }
        
        // Gets the N'th value from this range. This should be deterministic.
        public abstract object this[int index] { get; }

        public object RandomSample()
        {
            return this[Random.Range(0, NumValues)];
        }
    }
    
    // All continuous value types (float, Vector2, etc.) derived from RGContinuousValueRange
    public abstract class RGContinuousValueRange : IRGValueRange
    {
        public abstract object MinValue { get; }
        public abstract object MaxValue { get; }

        public abstract object RandomSample();
        
        // Divide this continuous range into N ranges
        public abstract RGContinuousValueRange[] Discretize(int n);
    }

    public class RGVoidRange : RGDiscreteValueRange
    {
        public override object MinValue => null;
        public override object MaxValue => null;
        public override int NumValues => 1;
        public override object this[int index] => null;
    }

    public class RGBoolRange : RGDiscreteValueRange
    {
        private int _minValue;
        private int _maxValue;

        public override object MinValue => _minValue != 0;
        public override object MaxValue => _maxValue != 0;
        
        public override int NumValues => _maxValue - _minValue + 1;

        public RGBoolRange(bool minValue, bool maxValue)
        {
            _minValue = minValue ? 1 : 0;
            _maxValue = maxValue ? 1 : 0;
            Debug.Assert(_minValue <= _maxValue);
        }

        public override object this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < NumValues);
                int val = _minValue + index;
                return val != 0;
            }
        }
    }

    public class RGFloatRange : RGContinuousValueRange
    {
        public override object MinValue { get; }
        public override object MaxValue { get; }
        
        // public RGFloatRange()
        
        public override object RandomSample()
        {
            return Random.Range()
        }

        public override RGContinuousValueRange[] Discretize(int n)
        {
            throw new NotImplementedException();
        }
    }
}