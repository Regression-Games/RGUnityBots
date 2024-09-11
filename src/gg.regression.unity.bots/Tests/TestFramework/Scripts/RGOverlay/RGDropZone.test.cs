using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

[TestFixture]
public class RGDropZoneTests
{
    private GameObject _uat;

    private RGDropZone dropZone;
    
    private readonly PointerEventData genericPointerEvent = new(EventSystem.current);

    [SetUp]
    public void SetUp()
    {
        _uat = new GameObject();
        dropZone = _uat.AddComponent<RGDropZone>();
        dropZone.transform.SetParent(_uat.transform, false);
        dropZone.droppables = new List<GameObject> { new() };
        var sequenceEditor = new GameObject();
        var sequenceEditorScript = sequenceEditor.AddComponent<RGSequenceEditor>();
        var input = sequenceEditorScript.gameObject.AddComponent<TMP_InputField>();
        sequenceEditorScript.NameInput = input;
        dropZone.SequenceEditor = sequenceEditor;
        dropZone.potentialDropSpotPrefab = new GameObject();
        dropZone.emptyStatePrefab = new GameObject();
        
        dropZone.Start();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(dropZone);
        Object.Destroy(_uat);
    }
    
    [Test]
    public void Start()
    {
        // Check for instance of RGSequenceEditor in SequenceEditor
        var sequenceEditorComponent = dropZone.SequenceEditor.GetComponent<RGSequenceEditor>();
        Assert.IsNotNull(sequenceEditorComponent, "SequenceEditor does not have RGSequenceEditor component");
    }
    
    [Test]
    public void AddChild()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        
        Assert.AreSame(newChild.transform.parent, dropZone.transform);
    }
    
    [Test]
    public void RemoveChild()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        
        Assert.AreSame(newChild.transform.parent, dropZone.transform);
        
        dropZone.RemoveChild(newChild);
        
        Assert.AreNotSame(newChild.transform.parent, dropZone.transform);
        Assert.IsNull(newChild.transform.parent);
    }
    
    [Test]
    public void Contains_True()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        
        Assert.IsTrue(dropZone.Contains(newChild));
    }
    
    [Test]
    public void Contains_False()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        var childThatIsNeverAdded = new GameObject();
        
        Assert.IsFalse(dropZone.Contains(childThatIsNeverAdded));
    }
    
    [Test]
    public void IsEmpty_True()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        dropZone.RemoveChild(newChild);
        
        Assert.IsTrue(dropZone.IsEmpty());
    }
    
    [Test]
    public void IsEmpty_False()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        
        Assert.IsFalse(dropZone.IsEmpty());
    }
    
    [Test]
    public void GetChildren()
    {
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
        
        var dropZoneChildren = dropZone.GetChildren();
        foreach (var child in children)
        {
            var hasChild = dropZoneChildren.Contains(child.GetComponent<RGDraggableCard>());
            Assert.IsTrue(hasChild);
        }
        
        Assert.AreEqual(numChildren, dropZoneChildren.Count);
    }
    
    [Test]
    public void ClearChildren()
    {
        var newChild = new GameObject();
        dropZone.AddChild(newChild);
        
        dropZone.ClearChildren();
        
        Assert.IsEmpty(dropZone.GetChildren());
        Assert.IsTrue(dropZone.IsEmpty());
    }

    [Test]
    public void OnPointerEnter_ValidDroppable()
    {
        PointerEventData pEvent = new(EventSystem.current);
        pEvent.pointerDrag = new GameObject();
        
        dropZone.OnPointerEnter(pEvent);
        
        Assert.IsTrue(dropZone.HasValidDroppable());
    }
    
    [Test]
    public void OnPointerEnter_InvalidDroppable()
    {
        PointerEventData pEvent = new(EventSystem.current);
        pEvent.pointerDrag = null;
        
        dropZone.OnPointerEnter(pEvent);
        
        Assert.IsFalse(dropZone.HasValidDroppable());
    }
    
    [Test]
    public void OnPointerExit()
    {
        var dragged = new GameObject();
        var draggableScript = dragged.AddComponent<RGDraggableCard>();
        PointerEventData pEvent = new(EventSystem.current);
        pEvent.pointerDrag = dragged;
        dropZone.OnPointerEnter(pEvent);
        
        dropZone.OnPointerExit(genericPointerEvent);
        
        Assert.IsFalse(draggableScript.IsOverDropZone());
        Assert.IsFalse(dropZone.HasValidDroppable());
    }
}