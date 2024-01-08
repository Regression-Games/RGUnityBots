using System;

namespace RegressionGames
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    // ReSharper disable InconsistentNaming
    public class RGStateTypeAttribute : Attribute
    {
        public static readonly Type Type = typeof(RGStateTypeAttribute);
        
        public string TypeName { get; }

        public bool IsPlayer { get; }
        
        public const RGStateIncludeFlags DefaultFlags = RGStateIncludeFlags.Field | RGStateIncludeFlags.Property |
                                                                  RGStateIncludeFlags.Public | RGStateIncludeFlags.Serializable;

        /**
         * <summary>Limit the properties of the call type to be included, Default is 'Field | Property | Public | Serializable</summary>
         */
        public RGStateIncludeFlags MyFlags { get; private set; } = DefaultFlags;

        // ReSharper disable once InvalidXmlDocComment
        /**
         * <summary>This constructor allows the attribute to specify RGStateIncludeFlags, e.g., [RGStateType(RGStateIncludeFlags.Public | RGStateIncludeFlags.Serializable | RGStateIncludeFlags.Field)]
         * Optionally you can also specify whether a game object with this behaviour represents a player avatar object.</summary>
         */
        public RGStateTypeAttribute(RGStateIncludeFlags includeFlags = DefaultFlags, bool isPlayer = false)
        {
            MyFlags = includeFlags;
            IsPlayer = isPlayer;
        }

        // ReSharper disable once InvalidXmlDocComment
        /**
         * <summary>
         * This constructor allows a type parameter and RGStateIncludeFlags to be specified, e.g., [RGStateType("SomeType", RGStateIncludeFlags.Public | RGStateIncludeFlags.Serializable | RGStateIncludeFlags.Field)].
         * This type is used in the state to override the classname being the type for this class.
         * Optionally you can also specify whether a game object with this behaviour represents a player avatar object.
         * </summary>
         */
        public RGStateTypeAttribute(string typeName, RGStateIncludeFlags includeFlags = DefaultFlags, bool isPlayer = false)
        {
            TypeName = typeName;
            MyFlags = includeFlags;
            IsPlayer = isPlayer;
        }
        
        [Flags]
        public enum RGStateIncludeFlags
        {
            NONE = 0,
            Public = 1,
            Serializable = 2,
            // Private = 4, TODO: Maybe support private properties later
            // Inherited = 8, TODO: Maybe support inherited properties later for subclassed MonoBehaviours
            Field = 16,
            Property = 32,
            Method = 64
        }
        
        
    }

}
