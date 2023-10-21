using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public static class GameObjectExtensions
    {
        /*
         * Cycles through all child game objects and calls 'SetActive(true)
         */
        public static void ActivateChildren(this GameObject parent)
        {
            if (parent == null)
            {
                Debug.LogWarning("Parent GameObject is null.");
                return;
            }

            Transform parentTransform = parent.transform;
            int childCount = parentTransform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform childTransform = parentTransform.GetChild(i);
                childTransform.gameObject.SetActive(true);
            }
        }

        /*
         * Cycles through all child game objects and calls 'SetActive(false)
         */
        public static void DeactivateChildren(this GameObject parent)
        {
            if (parent == null)
            {
                Debug.LogWarning("Parent GameObject is null.");
                return;
            }

            Transform parentTransform = parent.transform;
            int childCount = parentTransform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform childTransform = parentTransform.GetChild(i);
                childTransform.gameObject.SetActive(false);
            }
        }
    }
}