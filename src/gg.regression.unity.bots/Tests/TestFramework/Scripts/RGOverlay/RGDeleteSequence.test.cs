using System;
using NUnit.Framework;
using RegressionGames;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture]
public class RGDeleteSequenceTests
{
    private GameObject _uat;

    private RGDeleteSequence deleter;

    [SetUp]
    public void SetUp()
    {
        // create the delete script we want to test
        _uat = new GameObject();
        deleter = _uat.AddComponent<RGDeleteSequence>();
        deleter.sequenceNamePrefab = RGTestUtils.CreateTMProPlaceholder();
        deleter.confirmButton = new GameObject().AddComponent<Button>();
        deleter.cancelButton = new GameObject().AddComponent<Button>();
        deleter.Start();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_uat);
    }

    [Test]
    public void Start()
    {
        Assert.NotNull(deleter.confirmButton);
        Assert.NotNull(deleter.cancelButton);
    }
    
    [Test]
    public void Initialize()
    {
        var entry = new GameObject().AddComponent<RGSequenceEntry>();
        entry.sequenceName = "Great Sequence";
        entry.sequencePath = "/sequence/path";
        
        deleter.Initialize(entry);
        
        // ensure that we transfer the sequence entry's name and path to the script's display fields 
        Assert.AreEqual(entry.sequenceName, deleter.sequenceNamePrefab.text);
        Assert.AreEqual(entry.sequencePath, deleter.sequencePath);
    }
    
    [Test]
    public void OnDelete_HasSequencePath()
    {
        deleter.sequencePath = "/sequence/path";
        
        deleter.OnDelete();
        
        // ensure that we reset the script's display fields after deleting
        Assert.IsEmpty(deleter.sequencePath);
        Assert.IsEmpty(deleter.sequenceNamePrefab.text);
    }
    
    [Test]
    public void OnDelete_NoSequencePath()
    {
        deleter.sequencePath = null;
        
        // ensure that a missing sequence path throws an exception
        Assert.Throws<Exception>(() => deleter.OnDelete());
    }
    
    [Test]
    public void OnCancel()
    {
        // ensure that we can cancel the deletion and reset the scripts display fields 
        Assert.DoesNotThrow(() => deleter.OnCancel());
        Assert.IsEmpty(deleter.sequencePath);
        Assert.IsEmpty(deleter.sequenceNamePrefab.text);
    }
}