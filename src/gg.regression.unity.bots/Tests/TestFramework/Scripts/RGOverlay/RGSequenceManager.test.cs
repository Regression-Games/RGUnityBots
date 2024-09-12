using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;

[TestFixture]
public class RGSequenceManagerTests
{
    private GameObject _uat;
    
    private RGSequenceManager manager;

    [SetUp]
    public void SetUp()
    {
        // create the manager we want to test
        _uat = new GameObject();
        manager = _uat.AddComponent<RGSequenceManager>();
        manager.sequencesPanel = new GameObject();
        manager.sequenceCardPrefab = new GameObject();
        manager.sequenceEditor = new GameObject();
        manager.sequenceEditor.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_uat);
    }

    [Test]
    public void ShowEditSequenceDialog()
    {
        manager.ShowEditSequenceDialog();
        
        // ensure the sequence editor is visible
        Assert.IsTrue(manager.sequenceEditor.activeSelf);
    }
    
    [Test]
    public void HideEditSequenceDialog()
    {
        manager.HideEditSequenceDialog();
        
        // ensure the sequence editor is hidden
        Assert.IsFalse(manager.sequenceEditor.activeSelf);
    }
    
    [Test]
    public void InstantiateSequences()
    {
        // add a list of bot sequences to the manager for instantiation
        const int numSequences = 5;
        var sequences = new Dictionary<string, BotSequence>();
        for (var i = 0; i < numSequences; ++i)
        {
            sequences.Add("placeholder-path", new BotSequence());
        }

        manager.InstantiateSequences(sequences);

        // ensure that the sequences panel is populated with the correct number of prefabs
        Assert.AreEqual(numSequences, manager.sequencesPanel.transform.childCount);
    }
}
