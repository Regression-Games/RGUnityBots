using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RegressionGames.StateRecorder.Models
{
    public class TransformStatus : ObjectStatus
    {
        private TransformStatus()
        {}

        public readonly Transform Transform;

        // re-use these objects
        private static readonly StringBuilder PathBuilder = new (500);
        private static readonly StringBuilder NormalizedPathBuilder = new (500);
        private static readonly List<GameObject> ParentList = new(100);

        // right now this resets on awake from InGameObjectFinder, but we may have to deal with dynamically re-parented transforms better at some point...
        private static readonly Dictionary<int, TransformStatus> TransformsIveSeen = new(1000);

        private TransformStatus(Transform transform)
        {
            this.Transform = transform;
            this.Id = transform.GetInstanceID();
        }

        private TransformStatus(Transform transform, long id)
        {
            this.Transform = transform;
            this.Id = id;
        }

        public static void Reset()
        {
            TransformsIveSeen.Clear();
            ParentList.Clear();
        }

        public static TransformStatus GetOrCreateTransformStatus(Transform theTransform)
        {
            string tPath = null;
            string tPathNormalized = null;

            var id = theTransform.GetInstanceID();

            if (TransformsIveSeen.TryGetValue(id, out var status))
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

            var theGameObject = theTransform.gameObject;

            if (tPath == null || tPathNormalized == null)
            {
                // now .. get the path in the scene.. but only from 1 level down
                // iow.. ignore the name of the scene itself for cases where many scenes are loaded together like bossroom
                ParentList.Clear();
                ParentList.Add(theGameObject);
                var parent = theTransform.parent;
                while (parent != null)
                {
                    ParentList.Add(parent.gameObject);
                    parent = parent.parent;
                }

                PathBuilder.Clear();
                NormalizedPathBuilder.Clear();
                for (var i = ParentList.Count-1; i >=0; i--)
                {
                    var parentEntry = ParentList[i];
                    var objectName = parentEntry.gameObject.name;
                    PathBuilder.Append(objectName);
                    NormalizedPathBuilder.Append(SanitizeObjectName(objectName));
                    if (i - 1 >= 0)
                    {
                        PathBuilder.Append("/");
                        NormalizedPathBuilder.Append("/");
                    }
                }

                tPath = PathBuilder.ToString();
                tPathNormalized = NormalizedPathBuilder.ToString();

                if (status == null)
                {
                    status = new TransformStatus(theTransform, id)
                    {
                        ParentId = theTransform.parent != null ? theTransform.parent.GetInstanceID() : null,
                        LayerName = LayerMask.LayerToName(theGameObject.layer),
                        Scene = theGameObject.scene.name,
                        Tag = theTransform.tag
                    };
                    TransformsIveSeen[id] = status;
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

        public override bool PositionHitsCollider(Vector3 position)
        {
            var colliders = Transform.GetComponentsInChildren<Collider>();
            if (colliders.FirstOrDefault(a => a.bounds.Contains(position)) != null)
            {
                return true;
            }

            return false;
        }
    }
}
