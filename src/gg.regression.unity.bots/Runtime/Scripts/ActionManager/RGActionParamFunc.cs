using System;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// This represents a function that
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class RGActionParamFunc<T>
    {
        public Func<Object, T> Function { get; }

        public string Identifier { get; }
    }
}