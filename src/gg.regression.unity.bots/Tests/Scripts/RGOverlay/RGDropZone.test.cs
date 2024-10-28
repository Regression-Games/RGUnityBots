using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames;
using RegressionGames.TestFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGDropZoneTests
    {
        private GameObject _uat;

        private RGDropZone dropZone;

        private readonly PointerEventData genericPointerEvent = new(EventSystem.current);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // get a clean scene
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // destroy any existing overlay before loading new test scene
                Object.Destroy(botManager.gameObject);
            }

            // Wait for the scene
            SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("EmptyScene");


            // create the drop zone we want to test
            _uat = new GameObject();
            dropZone = _uat.AddComponent<RGDropZone>();
            dropZone.droppables = new List<GameObject> { new()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            } };

            dropZone.Content = new GameObject()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dropZone.Content.AddComponent<RectTransform>();
            dropZone.ScrollView = new GameObject()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dropZone.ScrollView.AddComponent<ScrollRect>();

            // create the sequence editor this drop zone requires, and any other required public fields
            var sequenceEditor = new GameObject()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            };
            var sequenceEditorScript = sequenceEditor.AddComponent<RGSequenceEditor>();
            var input = sequenceEditorScript.gameObject.AddComponent<TMP_InputField>();
            sequenceEditorScript.NameInput = input;
            dropZone.SequenceEditor = sequenceEditor;
            dropZone.potentialDropSpotPrefab = new GameObject()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            };
            dropZone.emptyStatePrefab = new GameObject()
            {
                transform =
                {
                    parent = dropZone.transform
                }
            };

            dropZone.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Start()
        {
            // ensure the drop zone initializes properly
            var sequenceEditorComponent = dropZone.SequenceEditor.GetComponent<RGSequenceEditor>();
            Assert.IsNotNull(sequenceEditorComponent, "SequenceEditor does not have RGSequenceEditor component");
        }

        [Test]
        public void AddChild()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);

            // ensure that the new child was properly attached as a child to the drop zone
            Assert.AreSame(newChild.transform.parent, dropZone.Content.transform);
        }

        [Test]
        public void RemoveChild()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);
            dropZone.RemoveChild(newChild);

            // ensure that the child has been removed from the drop zone, and that the child has no parent
            Assert.AreNotSame(newChild.transform.parent, dropZone.transform);
            Assert.IsNull(newChild.transform.parent);
        }

        [Test]
        public void Contains_True()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);

            // ensure the drop zone knows when it contains a specific child
            Assert.IsTrue(dropZone.Contains(newChild));
        }

        [Test]
        public void Contains_False()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);
            var childThatIsNeverAdded = new GameObject();

            // ensure that the drop zone does not provide false positives for knowing its children
            Assert.IsFalse(dropZone.Contains(childThatIsNeverAdded));
            Object.Destroy(childThatIsNeverAdded);
        }

        [Test]
        public void IsEmpty_True()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);
            dropZone.RemoveChild(newChild);

            // ensure that the drop zone has zero children
            Assert.IsTrue(dropZone.IsEmpty());
        }

        [Test]
        public void IsEmpty_False()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);

            // ensure that the drop zone has at least one child
            Assert.IsFalse(dropZone.IsEmpty());
        }

        [Test]
        public void SetEmptyState_IsEmpty()
        {
            dropZone.SetEmptyState(true);

            // ensure that the empty state was created
            Assert.IsTrue(dropZone.IsEmpty());
        }

        [Test]
        public void SetEmptyState_IsNotEmpty()
        {
            dropZone.SetEmptyState(false);

            // ensure that the empty state is not present
            Assert.IsFalse(dropZone.IsEmpty());
        }

        [Test]
        public void GetChildren()
        {
            // start by adding children to the drop zone
            var children = new List<GameObject>
            {
                new(),
                new(),
                new()
            };
            var numChildren = children.Count;
            foreach (var child in children)
            {
                child.AddComponent<RGDraggableCard>();
                dropZone.AddChild(child);
            }

            // check that each child was added to the drop zone properly
            var dropZoneChildren = dropZone.GetChildren();
            foreach (var child in children)
            {
                var hasChild = dropZoneChildren.Contains(child.GetComponent<RGDraggableCard>());
                Assert.IsTrue(hasChild);
            }

            Assert.AreEqual(numChildren, dropZoneChildren.Count);

            //cleanup
            dropZone.ClearChildren();
        }

        [Test]
        public void ClearChildren()
        {
            var newChild = new GameObject();
            dropZone.AddChild(newChild);
            dropZone.ClearChildren();

            // ensure that the drop zone removed its children properly, and that it tracks this state
            Assert.IsEmpty(dropZone.GetChildren());
            Assert.IsTrue(dropZone.IsEmpty());
        }

        [Test]
        public void OnPointerEnter_ValidDroppable()
        {
            PointerEventData pEvent = new(EventSystem.current);
            pEvent.pointerDrag = new GameObject(){
                transform =
                {
                    parent = dropZone.transform,
                },
            };

            dropZone.OnPointerEnter(pEvent);

            // ensure that the drop zone tracks when a droppable object is within its bounds
            Assert.IsTrue(dropZone.HasValidDroppable());
        }

        [Test]
        public void OnPointerEnter_InvalidDroppable()
        {
            PointerEventData pEvent = new(EventSystem.current);
            pEvent.pointerDrag = null;

            dropZone.OnPointerEnter(pEvent);

            // ensure that the drop zone does not track invalid droppable objects
            Assert.IsFalse(dropZone.HasValidDroppable());
        }

        [Test]
        public void OnPointerExit()
        {
            // create a draggable card
            var dragged = new GameObject();
            var draggableScript = dragged.AddComponent<RGDraggableCard>();

            // drag a card outside the drop zone
            PointerEventData pEvent = new(EventSystem.current);
            pEvent.pointerDrag = dragged;
            dropZone.OnPointerEnter(pEvent);
            dropZone.OnPointerExit(genericPointerEvent);

            // ensure that the card tracks that it is not over a drop zone, and that the drop zone tracks
            // if it has valid droppable objects within its bounds
            Assert.IsFalse(draggableScript.IsOverDropZone());
            Assert.IsFalse(dropZone.HasValidDroppable());

            Object.Destroy(dragged);
        }


    }
}
