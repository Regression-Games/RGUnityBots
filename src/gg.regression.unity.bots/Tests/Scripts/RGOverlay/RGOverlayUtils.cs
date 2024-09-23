using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
            var sequenceEditor = new GameObject();
            var sequenceEditorScript = sequenceEditor.AddComponent<RGSequenceEditor>();
            var dzTextObject = new GameObject();
            var dzText = dzTextObject.AddComponent<TMP_InputField>();
            sequenceEditorScript.NameInput = dzText;

            var dropZone = new GameObject();
            dropZone.transform.SetParent(root.transform, false);
            dropZone.gameObject.AddComponent<RectTransform>();
            var dzScript = dropZone.AddComponent<RGDropZone>();
            dzScript.Content = new GameObject();
            dzScript.Content.AddComponent<RectTransform>();
            dzScript.ScrollView = new GameObject();
            dzScript.ScrollView.AddComponent<ScrollRect>();
            dzScript.potentialDropSpotPrefab = new GameObject();
            dzScript.emptyStatePrefab = new GameObject();
            dzScript.SequenceEditor = sequenceEditor;
            dzScript.droppables = new List<GameObject>() { new() };
            dzScript.Start();
            return dropZone;
        }
    }
}
