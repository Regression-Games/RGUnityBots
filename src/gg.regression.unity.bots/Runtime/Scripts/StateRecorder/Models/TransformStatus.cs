using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    public class TransformStatus
    {
        private TransformStatus()
        {

        }

        public override string ToString()
        {
            // implement for easier debugger usage
            return "" + Id + " - " + Path + " - " + rendererCount + " - " + (screenSpaceBounds!=null?"true":"false") + " - " + (worldSpaceBounds !=null?"true":"false");
        }

        public int Id;
        public bool? HasKeyTypes;
        public string Path;
        /**
         * <summary>Has things like ' (1)' and ' (Clone)' stripped off of object names.</summary>
         */
        public string NormalizedPath;

        public Transform Transform;

        /**
         * <summary>cached pointer to the top level transform of this transform.. must check != null to avoid stale unity object references</summary>
         */
        public Transform TopLevelForThisTransform;

        public int rendererCount;

        public Bounds? screenSpaceBounds;
        /**
         * <summary>The closest distance to the camera, tracked outside of screenSpaceBounds so that screen space bounds is always around 0.0</summary>
         */
        public float screenSpaceZOffset;

        public Bounds? worldSpaceBounds;


        // re-use these objects
        private static readonly StringBuilder _tPathBuilder = new StringBuilder(500);
        private static readonly StringBuilder _tNormalizedPathBuilder = new StringBuilder(500);
        private static readonly List<GameObject> _parentList = new(100);

        // right now this resets on awake from InGameObjectFinder, but we may have to deal with dynamically re-parented transforms better at some point...
        private static readonly Dictionary<int, TransformStatus> _transformsIveSeen = new(1000);

        public static void Reset()
        {
            _transformsIveSeen.Clear();
            _parentList.Clear();
        }

        public static TransformStatus GetOrCreateTransformStatus(Transform theTransform)
        {
            string tPath = null;
            string tPathNormalized = null;

            var id = theTransform.GetInstanceID();

            if (_transformsIveSeen.TryGetValue(id, out var status))
            {
                if (status.Path != null)
                {
                    tPath = status.Path;
                }

                if (status.NormalizedPath != null)
                {
                    tPathNormalized = status.NormalizedPath;
                }
            }

            if (tPath == null || tPathNormalized == null)
            {
                // now .. get the path in the scene.. but only from 1 level down
                // iow.. ignore the name of the scene itself for cases where many scenes are loaded together like bossroom
                _parentList.Clear();
                _parentList.Add(theTransform.gameObject);
                var parent = theTransform.parent;
                while (parent != null)
                {
                    _parentList.Add(parent.gameObject);
                    parent = parent.parent;
                }

                _tPathBuilder.Clear();
                _tNormalizedPathBuilder.Clear();
                for (var i = _parentList.Count-1; i >=0; i--)
                {
                    var parentEntry = _parentList[i];
                    var objectName = parentEntry.gameObject.name;
                    _tPathBuilder.Append(objectName);
                    _tNormalizedPathBuilder.Append(SanitizeObjectName(objectName));
                    if (i - 1 >= 0)
                    {
                        _tPathBuilder.Append("/");
                        _tNormalizedPathBuilder.Append("/");
                    }
                }

                tPath = _tPathBuilder.ToString();
                tPathNormalized = _tNormalizedPathBuilder.ToString();

                if (status == null)
                {
                    status = new TransformStatus
                    {
                        Id = id,
                        Transform = theTransform
                    };
                    _transformsIveSeen[id] = status;
                    // update the cache with our result
                }
                status.Path = tPath;
                status.NormalizedPath = tPathNormalized;
            }

            return status;
        }

        private static string SanitizeObjectName(string objectName)
        {
            objectName = FastTrim(objectName);
            // Removes '(Clone)' and ' (1)' uniqueness numbers for copies
            // may also remove some valid naming pieces like (TMP).. but oh well, REGEX for this performed horribly
            while (objectName.EndsWith(')'))
            {
                var li = objectName.LastIndexOf('(');
                if (li > 0)
                {
                    objectName = FastTrim(objectName.Substring(0, li));
                }
            }

            return objectName;
        }

        private static string FastTrim(string input)
        {
            var index = input.Length - 1;
            while (input[index] == ' ')
            {
                --index;
            }

            if (index != input.Length - 1)
            {
                return input.Substring(0, index + 1);
            }

            return input;
        }
    }
}
