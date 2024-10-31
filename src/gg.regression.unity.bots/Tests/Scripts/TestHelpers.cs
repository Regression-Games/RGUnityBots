using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames.Tests
{
    public class TestHelpers
    {
        /** <summary>
         * Creates a placeholder for an instance of Sprite
         * </summary>
         */
        public static Sprite CreateSpritePlaceholder()
        {
            return Sprite.Create(
                new Texture2D(10, 10),
                new Rect(0, 0, 10, 10),
                new Vector2(0.5f, 0.5f),
                100
            );
        }

        /** <summary>
         * Creates a placeholder for an instance of TMP_Text. This can be used to mock TMPro inputs
         * </summary>
         */
        public static TMP_Text CreateTMProPlaceholder(Transform _parent)
        {
            var textObject = new GameObject()
            {
                transform =
                {
                    parent = _parent.transform
                }
            };
            var placeholder = textObject.AddComponent<TextPlaceholder>();
            return placeholder;
        }

        /** <summary>
         * Implements the TMP_Text abstract class, in order to act as a placeholder for TMPro inputs
         * </summary>
         */
        private class TextPlaceholder : TMP_Text
        {
            protected override void OnPopulateMesh(VertexHelper toFill)
            {
                toFill.Clear();
            }
        }
    }
}
