using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.ActionManager
{
    public enum RGValueRangeType
    {
        RANGE_VOID,
        RANGE_BOOL,
        RANGE_INT,
        RANGE_VECTOR2_INT,
        RANGE_FLOAT,
        RANGE_VECTOR2
    }
    
    // This class is used to define the range of acceptable values for action parameters.
    [JsonConverter(typeof(RGValueRangeJsonConverter))]
    public interface IRGValueRange
    {
        public RGValueRangeType Type { get; }
        public object MinValue { get; }
        public object MaxValue { get; }
        public object RandomSample();
        public bool RangeEquals(IRGValueRange other);
        public void WriteToStringBuilder(StringBuilder stringBuilder);
    }
    
    // All discrete value types (bool, char, int, etc.) derived from RGDiscreteValueRange
    public abstract class RGDiscreteValueRange : IRGValueRange
    {
        public abstract RGValueRangeType Type { get; }
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

        public abstract void WriteToStringBuilder(StringBuilder stringBuilder);
    }
    
    // All continuous value types (float, Vector2, etc.) derived from RGContinuousValueRange
    public abstract class RGContinuousValueRange : IRGValueRange
    {
        public abstract RGValueRangeType Type { get; }
        public abstract object MinValue { get; }
        public abstract object MidPoint { get; }
        public abstract object MaxValue { get; }

        public abstract object RandomSample();
        
        // Divide this continuous range into N ranges
        public abstract RGContinuousValueRange[] Discretize(int n);
        
        public abstract bool RangeEquals(IRGValueRange other);
        
        public abstract void WriteToStringBuilder(StringBuilder stringBuilder);
    }

    public class RGVoidRange : RGDiscreteValueRange
    {
        public override RGValueRangeType Type => RGValueRangeType.RANGE_VOID;
        public override object MinValue => null;
        public override object MaxValue => null;
        public override int NumValues => 1;
        public override object this[int index] => null;

        public RGVoidRange(JObject serializedRange)
        {
        }
        
        public override bool RangeEquals(IRGValueRange other)
        {
            return other is RGVoidRange;
        }

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            return "void";
        }
    }

    public class RGBoolRange : RGDiscreteValueRange
    {
        public override RGValueRangeType Type => RGValueRangeType.RANGE_BOOL;
        
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
        
        public RGBoolRange(JObject serializedRange)
        {
            _minValue = serializedRange["minValue"].ToObject<int>();
            _maxValue = serializedRange["maxValue"].ToObject<int>();
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

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append(",\"minValue\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _minValue);
            stringBuilder.Append(",\"maxValue\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue);
            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            return $"bool ({MinValue}, {MaxValue})";
        }
    }

    public class RGVector2IntRange : RGDiscreteValueRange
    {
        public override RGValueRangeType Type => RGValueRangeType.RANGE_VECTOR2_INT;
        
        private Vector2Int _minValue;
        private Vector2Int _maxValue;
        
        public override object MinValue { get; }
        public override object MaxValue { get; }

        public RGVector2IntRange(Vector2Int minValue, Vector2Int maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }
        
        public RGVector2IntRange(JObject serializedRange)
        {
            int minValueX = serializedRange["minValueX"].ToObject<int>();
            int minValueY = serializedRange["minValueY"].ToObject<int>();
            int maxValueX = serializedRange["maxValueX"].ToObject<int>();
            int maxValueY = serializedRange["maxValueY"].ToObject<int>();
            _minValue = new Vector2Int(minValueX, minValueY);
            _maxValue = new Vector2Int(maxValueX, maxValueY);
        }
        
        public override bool RangeEquals(IRGValueRange other)
        {
            if (other is RGVector2IntRange v2Range)
            {
                return _minValue == v2Range._minValue && _maxValue == v2Range._maxValue;
            }
            else
            {
                return false;
            }
        }

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append(",\"minValueX\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _minValue.x);
            stringBuilder.Append(",\"minValueY\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _minValue.y);
            stringBuilder.Append(",\"maxValueX\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue.x);
            stringBuilder.Append(",\"maxValueY\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue.y);
            stringBuilder.Append("}");
        }

        public int Width => (_maxValue.x - _minValue.x + 1);
        public int Height => (_maxValue.y - _minValue.y + 1);

        public override int NumValues => Width * Height;

        public override object this[int index]
        {
            get
            {
                int xi = index % Width;
                int yi = index / Width;
                return new Vector2Int(_minValue.x + xi, _minValue.y + yi);
            }
        }
    }

    public class RGIntRange : RGDiscreteValueRange
    {
        public override RGValueRangeType Type => RGValueRangeType.RANGE_INT;
        
        private int _minValue;
        private int _maxValue;

        public override object MinValue => _minValue;
        public override object MaxValue => _maxValue;

        public RGIntRange(int minValue, int maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }
        
        public RGIntRange(JObject serializedRange)
        {
            _minValue = serializedRange["minValue"].ToObject<int>();
            _maxValue = serializedRange["maxValue"].ToObject<int>();
        }
        
        public override bool RangeEquals(IRGValueRange other)
        {
            if (other is RGIntRange intRange)
            {
                return _minValue == intRange._minValue && _maxValue == intRange._maxValue;
            }
            else
            {
                return false;
            }
        }

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append(",\"minValue\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _minValue);
            stringBuilder.Append(",\"maxValue\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue);
            stringBuilder.Append("}");
        }

        public override int NumValues => _maxValue - _minValue + 1;

        public override object this[int index] => _minValue + index;

        public override string ToString()
        {
            return $"int ({MinValue}, {MaxValue})";
        }
    }

    public class RGFloatRange : RGContinuousValueRange
    {
        public override RGValueRangeType Type => RGValueRangeType.RANGE_FLOAT;
        
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
        
        public RGFloatRange(JObject serializedRange)
        {
            _minValue = serializedRange["minValue"].ToObject<float>();
            _maxValue = serializedRange["maxValue"].ToObject<float>();
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

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append(",\"minValue\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _minValue);
            stringBuilder.Append(",\"maxValue\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue);
            stringBuilder.Append("}");
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
        public override RGValueRangeType Type => RGValueRangeType.RANGE_VECTOR2;
        
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

        public RGVector2Range(JObject serializedRange)
        {
            float minValueX = serializedRange["minValueX"].ToObject<float>();
            float minValueY = serializedRange["minValueY"].ToObject<float>();
            float maxValueX = serializedRange["maxValueX"].ToObject<float>();
            float maxValueY = serializedRange["maxValueY"].ToObject<float>();
            _minValue = new Vector2(minValueX, minValueY);
            _maxValue = new Vector2(maxValueX, maxValueY);
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

        public override void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, Type.ToString());
            stringBuilder.Append(",\"minValueX\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _minValue.x);
            stringBuilder.Append(",\"minValueY\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _minValue.y);
            stringBuilder.Append(",\"maxValueX\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue.x);
            stringBuilder.Append(",\"maxValueY\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, _maxValue.y);
            stringBuilder.Append("}");
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