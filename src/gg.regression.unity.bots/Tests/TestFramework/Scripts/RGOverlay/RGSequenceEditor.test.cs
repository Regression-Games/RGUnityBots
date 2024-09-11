using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames;
using RegressionGames.StateRecorder.BotSegments.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture]
public class RGSequenceEditorTests
{
    private GameObject _uat;

    private RGSequenceEditor editor;

    private IList<BotSequenceEntry> segments;

    [SetUp]
    public void SetUp()
    {
        _uat = new GameObject();
        editor = _uat.AddComponent<RGSequenceEditor>();
        editor.transform.SetParent(_uat.transform, false);

        var textObject = new GameObject();
        var text = textObject.AddComponent<TMP_InputField>();
        editor.NameInput = text;
        editor.DescriptionInput = text;
        editor.SearchInput = text;
        editor.AvailableSegmentsList = new GameObject();
        editor.CreateSequenceButton = new GameObject();
        editor.CreateSequenceButton.AddComponent<Button>();
        editor.SegmentCardPrefab = new GameObject();
        editor.DropZonePrefab = new GameObject();
        var dropZone = editor.DropZonePrefab.AddComponent<RGDropZone>();
        dropZone.emptyStatePrefab = new GameObject();
        editor.SegmentListIcon = RGTestUtils.CreateSpritePlaceholder();
        editor.SegmentIcon = RGTestUtils.CreateSpritePlaceholder();
        editor.SegmentListIcon = RGTestUtils.CreateSpritePlaceholder();
        
        segments = new List<BotSequenceEntry>()
        {
            new()
            {
                type = BotSequenceEntryType.Segment,
                path = "segment/one",
                entryName = "Segment One",
                description = "Segment One Description"
            },
            new()
            {
                type = BotSequenceEntryType.Segment,
                path = "segment/two",
                entryName = "Segment Two",
                description = "Segment Two Description",
            },
            new()
            {
                type = BotSequenceEntryType.SegmentList,
                path = "segment/three/list",
                entryName = "Segment Three",
                description = "Segment Three Description",
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(editor);
        Object.Destroy(_uat);
    }

    [Test]
    public void Initialize()
    {
        editor.Initialize();

        Assert.NotNull(editor.NameInput.onValueChanged);
        Assert.NotNull(editor.SearchInput.onValueChanged);
        Assert.NotNull(editor.CurrentSequence);
    }
    
    [Test]
    public void CreateAvailableSegments()
    {
        editor.CreateAvailableSegments(segments);

        Assert.AreEqual(segments.Count, editor.AvailableSegmentsList.transform.childCount);
    }
    
    [Test]
    public void ClearAvailableSegments()
    {
        editor.CreateAvailableSegments(segments);
        var numSegmentsCleared = editor.ClearAvailableSegments();

        Assert.AreEqual(segments.Count, numSegmentsCleared);
    }
    
    [Test]
    public void SaveSequence()
    {
        editor.Initialize();
        
        editor.NameInput.text = "Sequence Editor Name";
        editor.DescriptionInput.text = "Sequence Editor Description";
        
        editor.SaveSequence();
        
        Assert.AreEqual(editor.CurrentSequence.name, editor.NameInput.text);
        Assert.AreEqual(editor.CurrentSequence.description, editor.DescriptionInput.text);
    }
    
    [Test]
    public void ResetEditor()
    {
        editor.Initialize();

        editor.NameInput.text = "Sequence Editor Name";
        editor.DescriptionInput.text = "Sequence Editor Description";
        
        editor.ResetEditor();
        
        var dropZone = editor.DropZonePrefab.GetComponent<RGDropZone>();
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        
        Assert.IsEmpty(editor.NameInput.text);
        Assert.IsEmpty(editor.DescriptionInput.text);
        Assert.IsEmpty(dropZone.GetChildren());
        Assert.IsFalse(button.interactable);
    }

    [Test]
    public void ReloadAvailableSegments()
    {
        editor.Initialize();
        
        editor.ReloadAvailableSegments();
        
        Assert.IsEmpty(editor.SearchInput.text);
    }

    [Test]
    public void SetCreateSequenceButtonEnabled_Enabled()
    {
        editor.Initialize();
        
        editor.SetCreateSequenceButtonEnabled(true);
        
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        Assert.IsTrue(button.interactable);
    }
    
    [Test]
    public void SetCreateSequenceButtonEnabled_Disabled()
    {
        editor.Initialize();
        
        editor.SetCreateSequenceButtonEnabled(false);
        
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        Assert.IsFalse(button.interactable);
    }
}