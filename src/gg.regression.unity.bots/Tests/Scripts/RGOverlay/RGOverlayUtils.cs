using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.Tests.RGOverlay
{
    public class RGOverlayUtils
    {
        /**
         * <summary>
         * Create and initialize a new Drop Zone that accepts any Game Object as a child
         * </summary>
         */
        public static GameObject CreateNewDropZone(GameObject root)
        {
            var sequenceEditor = new GameObject("Mock_SequenceEditor");
            var sequenceEditorScript = sequenceEditor.AddComponent<RGSequenceEditor>();
            var dzTextObject = new GameObject("Mock_DropZoneText"){
                transform =
                {
                    parent = sequenceEditor.transform
                }
            };
            var dzText = dzTextObject.AddComponent<TMP_InputField>();
            sequenceEditorScript.NameInput = dzText;

            var dropZone = new GameObject(){
                transform =
                {
                    parent = sequenceEditor.transform
                }
            };
            dropZone.gameObject.AddComponent<RectTransform>();
            var dzScript = dropZone.AddComponent<RGDropZone>();
            dzScript.Content = new GameObject("Mock_DropZoneContent"){
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dzScript.Content.AddComponent<RectTransform>();
            dzScript.ScrollView = new GameObject("Mock_DropZoneScrollView"){
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dzScript.ScrollView.AddComponent<ScrollRect>();
            dzScript.potentialDropSpotPrefab = new GameObject("Mock_DropZonePotentialDropSpotPrefab"){
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dzScript.emptyStatePrefab = new GameObject("Mock_DropZoneEmptyStatePrefabPrefab"){
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dzScript.SequenceEditor = sequenceEditor;
            dzScript.droppables = new List<GameObject>() { new("Mock_DropZoneDroppable"){
                transform =
                {
                    parent = dropZone.transform
                }
            }

            };
            dzScript.Start();
            return dropZone;
        }
    }
}
