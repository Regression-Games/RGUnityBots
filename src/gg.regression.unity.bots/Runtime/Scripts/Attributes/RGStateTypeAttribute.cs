using System;

namespace RegressionGames
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    // ReSharper disable InconsistentNaming
    public class RGStateTypeAttribute : Attribute
    {
        public static readonly Type Type = typeof(RGStateTypeAttribute);

        public string TypeName { get; }

        public bool IsPlayer { get; set; }

        //Include Fields and Properties only by default
        public const RGStateIncludeFlags DefaultFlags = RGStateIncludeFlags.Field | RGStateIncludeFlags.Property;

        /**
         * <summary>Limit the properties of the call type to be included, Default is 'Field | Property'</summary>
         */
        public RGStateIncludeFlags IncludeFlags { get; set; } = DefaultFlags;


        public RGStateTypeAttribute()
        {
        }

        /**
         * <summary>
         * This constructor allows a type parameter to be specified, e.g., [RGStateType("SomeType")].
         * This type is used in the state to override the classname being the type for this class.
         * </summary>
         */
        public RGStateTypeAttribute(string typeName)
        {
            TypeName = typeName;
        }

        [Flags]
        public enum RGStateIncludeFlags
        {
            NONE = 0b0000_0000,
            // Maybe support limiting visibility of properties later; for now just public
            // Public = 0b0000_0001,
            // Serializable = 0b0000_0010,
            // Private = 0b0000_0100,
            Field = 0b0001_0000,
            Property = 0b0010_0000,
            Method = 0b0100_0000
        }


    }

}
