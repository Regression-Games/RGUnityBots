using System;

namespace RegressionGames
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    // ReSharper disable InconsistentNaming
    public class RGStateTypeAttribute : Attribute
    {
        public static readonly Type Type = typeof(RGStateTypeAttribute);

        public readonly string TypeName;

        public readonly bool IsPlayer;
        
        //Include Fields and Properties only by default
        public const RGStateIncludeFlags DefaultFlags = RGStateIncludeFlags.Field | RGStateIncludeFlags.Property;

        /**
         * <summary>Limit the properties of the call type to be included, Default is 'Field | Property'</summary>
         */
        public readonly RGStateIncludeFlags MyFlags;
        
        // ReSharper disable once InvalidXmlDocComment
        /**
         * <summary>This constructor allows the attribute to specify RGStateIncludeFlags, e.g., [RGStateType(RGStateIncludeFlags.Field)]
         * Optionally you can also specify whether a game object with this behaviour represents a player avatar object.</summary>
         */
        public RGStateTypeAttribute(bool isPlayer = false)
        {
            MyFlags = DefaultFlags;
            IsPlayer = isPlayer;
        }

        // ReSharper disable once InvalidXmlDocComment
        /**
         * <summary>This constructor allows the attribute to specify RGStateIncludeFlags, e.g., [RGStateType(RGStateIncludeFlags.Field)]
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
         * This constructor allows a type parameter and RGStateIncludeFlags to be specified, e.g., [RGStateType("SomeType", RGStateIncludeFlags.Field)].
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
            // Maybe support limiting visibility of properties later; for now just public
            // Public = 1,
            // Serializable = 2,
            // Private = 4,
            Field = 16,
            Property = 32,
            Method = 64
        }
        
        
    }

}
