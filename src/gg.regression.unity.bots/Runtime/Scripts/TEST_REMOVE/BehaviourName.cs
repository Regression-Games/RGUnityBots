using UnityEngine;

namespace RegressionGames.TEST_REMOVE
{
    [RGStateType("BehaviourNameTypeTest", true)]
    public class BehaviourName : MonoBehaviour
    {
        [RGState("spatialOrientation")]
        public Vector3? SpatialOrientation = Vector3.zero;

        [RGState("spatialRotation")]
        public Quaternion SpatialRotation = Quaternion.identity;

        [RGState("spatialBounds")]
        public Bounds SpatialBounds => new Bounds();
        
        [RGState("spatialSize")]
        public Vector2Int SpatialSize()
        {
            return Vector2Int.one;
        }

        [RGState] public long? nullableLong = 0;
        [RGState] public short someShort = 0;

        public bool SomeNonStatefulField = true;

        [RGAction("moveInSpace")]
        public void MoveInSpace(Vector3? newOrientation)
        {
            SpatialOrientation = newOrientation;
        }
        
        [RGAction]
        public void ChangeRotation(Quaternion newRotation)
        {
            SpatialRotation = newRotation;
        }
        
        [RGAction]
        public void ChangeNullableLong(long? nullableLong)
        {
            this.nullableLong = nullableLong;
        }

        [RGAction]
        public short ChangeSomeShort(short someShort)
        {
            this.someShort = someShort;
            return this.someShort;
        }
    }
}
