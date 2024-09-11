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
        // create the editor we want to test
        _uat = new GameObject();
        editor = _uat.AddComponent<RGSequenceEditor>();
        editor.transform.SetParent(_uat.transform, false);

        // create the required public fields the editor needs
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
        
        // mock segment entries for the editor to utilize
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
        Object.Destroy(_uat);
    }

    [Test]
    public void Initialize()
    {
        editor.Initialize();

        // ensure that the editor consumes its public fields properly
        Assert.NotNull(editor.NameInput.onValueChanged);
        Assert.NotNull(editor.SearchInput.onValueChanged);
        Assert.NotNull(editor.CurrentSequence);
    }
    
    [Test]
    public void CreateAvailableSegments()
    {
        editor.CreateAvailableSegments(segments);

        // ensure that the mock segments were instantiated as prefabs properly
        Assert.AreEqual(segments.Count, editor.AvailableSegmentsList.transform.childCount);
    }
    
    [Test]
    public void ClearAvailableSegments()
    {
        editor.CreateAvailableSegments(segments);
        var numSegmentsCleared = editor.ClearAvailableSegments();

        // ensure that the number of segments removed matches the expected amount
        Assert.AreEqual(segments.Count, numSegmentsCleared);
    }
    
    [Test]
    public void SaveSequence()
    {
        editor.Initialize();
        
        // set the name and description values to save
        editor.NameInput.text = "Sequence Editor Name";
        editor.DescriptionInput.text = "Sequence Editor Description";
        
        editor.SaveSequence();
        
        // ensure that the editor extracts the values from the text inputs properly
        Assert.AreEqual(editor.CurrentSequence.name, editor.NameInput.text);
        Assert.AreEqual(editor.CurrentSequence.description, editor.DescriptionInput.text);
    }
    
    [Test]
    public void ResetEditor()
    {
        editor.Initialize();

        // set the name and description values we want to reset
        editor.NameInput.text = "Sequence Editor Name";
        editor.DescriptionInput.text = "Sequence Editor Description";
        
        editor.ResetEditor();
        
        var dropZone = editor.DropZonePrefab.GetComponent<RGDropZone>();
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        
        // ensure that the name and description text inputs are cleared, the drop zone's children are cleared, and that
        // the create sequence button is disabled
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
        
        // ensure that the create sequence button is enabled
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        Assert.IsTrue(button.interactable);
    }
    
    [Test]
    public void SetCreateSequenceButtonEnabled_Disabled()
    {
        editor.Initialize();
        
        editor.SetCreateSequenceButtonEnabled(false);
        
        // ensure that the create sequence button is disabled
        var button = editor.CreateSequenceButton.GetComponent<Button>();
        Assert.IsFalse(button.interactable);
    }
}