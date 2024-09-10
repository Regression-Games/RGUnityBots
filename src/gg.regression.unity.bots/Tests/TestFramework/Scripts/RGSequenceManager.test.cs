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
        Object.Destroy(manager.sequencesPanel);
        Object.Destroy(manager.sequenceCardPrefab);
        Object.Destroy(manager.sequenceEditor);
        Object.Destroy(manager);
        Object.Destroy(_uat);
    }

    [Test]
    public void Initialization()
    {
        Assert.NotNull(_uat);
        Assert.NotNull(manager);
    }

    [Test]
    public void ShowEditSequenceDialog()
    {
        manager.ShowEditSequenceDialog();
        Assert.IsTrue(manager.sequenceEditor.activeSelf);
    }
    
    [Test]
    public void HideEditSequenceDialog()
    {
        manager.HideEditSequenceDialog();
        Assert.IsFalse(manager.sequenceEditor.activeSelf);
    }
    
    [Test]
    public void InstantiateSequences()
    {
        const int numSequences = 5;
        var sequences = new List<BotSequence>();
        for (var i = 0; i < numSequences; ++i)
        {
            sequences.Add(new BotSequence());
        }

        manager.InstantiateSequences(sequences);

        Assert.AreEqual(numSequences, manager.sequencesPanel.transform.childCount);
    }
}
