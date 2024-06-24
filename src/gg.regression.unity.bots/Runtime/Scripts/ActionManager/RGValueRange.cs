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
        public bool RangeEquals(IRGValueRange other);
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

        public abstract bool RangeEquals(IRGValueRange other);
    }
    
    // All continuous value types (float, Vector2, etc.) derived from RGContinuousValueRange
    public abstract class RGContinuousValueRange : IRGValueRange
    {
        public abstract object MinValue { get; }
        public abstract object MidPoint { get; }
        public abstract object MaxValue { get; }

        public abstract object RandomSample();
        
        // Divide this continuous range into N ranges
        public abstract RGContinuousValueRange[] Discretize(int n);
        
        public abstract bool RangeEquals(IRGValueRange other);
    }

    public class RGVoidRange : RGDiscreteValueRange
    {
        public override object MinValue => null;
        public override object MaxValue => null;
        public override int NumValues => 1;
        public override object this[int index] => null;
        
        public override bool RangeEquals(IRGValueRange other)
        {
            return other is RGVoidRange;
        }

        public override string ToString()
        {
            return "void";
        }
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

        public RGBoolRange() : this(false, true)
        {
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
        
        public override bool RangeEquals(IRGValueRange other)
        {
            if (other is RGBoolRange boolRange)
            {
                return _minValue == boolRange._minValue && _maxValue == boolRange._maxValue;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return $"bool ({MinValue}, {MaxValue})";
        }
    }

    public class RGFloatRange : RGContinuousValueRange
    {
        private float _minValue;
        private float _maxValue;

        public override object MinValue => _minValue;
        public override object MidPoint => (_minValue + _maxValue) / 2.0f;
        public override object MaxValue => _maxValue;

        public RGFloatRange(float minValue, float maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            Debug.Assert(_minValue <= _maxValue);
        }
        
        public override object RandomSample()
        {
            return Random.Range(_minValue, _maxValue);
        }

        public override bool RangeEquals(IRGValueRange other)
        {
            if (other is RGFloatRange floatRange)
            {
                return Mathf.Approximately(_minValue, floatRange._minValue) &&
                       Mathf.Approximately(_maxValue, floatRange._maxValue);
            }
            else
            {
                return false;
            }
        }

        public override RGContinuousValueRange[] Discretize(int n)
        {
            float stepSize = (_maxValue - _minValue) / n;
            RGContinuousValueRange[] result = new RGContinuousValueRange[n];
            for (int i = 0; i < n; ++i)
            {
                float minVal = _minValue + i * stepSize;
                result[i] = new RGFloatRange(minVal, minVal + stepSize);
            }
            return result;
        }

        public override string ToString()
        {
            return $"float ({MinValue}, {MaxValue})";
        }
    }

    public class RGVector2Range : RGContinuousValueRange
    {
        private Vector2 _minValue;
        private Vector2 _maxValue;

        public override object MinValue => _minValue;
        public override object MidPoint => (_minValue + _maxValue) / 2.0f;
        public override object MaxValue => _maxValue;
        
        public RGVector2Range(Vector2 minValue, Vector2 maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            Debug.Assert(minValue.x <= maxValue.x);
            Debug.Assert(minValue.y <= maxValue.y);
        }
        
        public override object RandomSample()
        {
            return new Vector2(Random.Range(_minValue.x, _maxValue.x),
                Random.Range(_minValue.y, _maxValue.y));
        }

        public override bool RangeEquals(IRGValueRange other)
        {
            if (other is RGVector2Range v2Range)
            {
                return _minValue == v2Range._minValue && _maxValue == v2Range._maxValue;
            }
            else
            {
                return false;
            }
        }

        public override RGContinuousValueRange[] Discretize(int n)
        {
            RGContinuousValueRange[] result = new RGContinuousValueRange[n];
            
            int sq = (int)Math.Sqrt(n);
            if (sq * sq != n)
            {
                throw new ArgumentException("Discretization of RGVector2Range can only be done with square values of n");
            }

            float stepX = (_maxValue.x - _minValue.x) / sq;
            float stepY = (_maxValue.y - _minValue.y) / sq;
            for (int i = 0; i < sq; ++i)
            {
                for (int j = 0; j < sq; ++j)
                {
                    float minX = _minValue.x + j * stepX;
                    float minY = _minValue.y + i * stepY;
                    result[i*sq + j] = new RGVector2Range(new Vector2(minX, minY), new Vector2(minX + stepX, minY + stepY));
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"Vector2 ({MinValue}, {MaxValue})";
        }
    }
}