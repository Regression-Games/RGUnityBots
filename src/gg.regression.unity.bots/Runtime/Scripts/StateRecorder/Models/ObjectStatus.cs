using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    public abstract class ObjectStatus
    {
        public override string ToString()
        {
            // implement for easier debugger usage
            return "" + Id + " - " + ParentId + " - " + Path + " - " + (screenSpaceBounds!=null?"true":"false") + " - " + (worldSpaceBounds !=null?"true":"false");
        }

        public long Id;

        public long? ParentId;
        public string Path;
        public string Tag; //Not currently supported in ECS
        public string LayerName;
        public string Scene;

        /**
         * <summary>Has things like ' (1)' and ' (Clone)' stripped off of object names.</summary>
         */
        public string NormalizedPath;

        /**
         * Used by key moments evaluation to cache path tokenization for performance
         */
        [NonSerialized]
        public List<string[]> TokenizedObjectPath = null;

        public Bounds? screenSpaceBounds;
        /**
         * <summary>The closest distance to the camera, tracked outside of screenSpaceBounds so that screen space bounds is always around 0.0</summary>
         */
        public float screenSpaceZOffset;

        public Bounds? worldSpaceBounds;

        public abstract bool PositionHitsCollider(Vector3 position);

    }
}
