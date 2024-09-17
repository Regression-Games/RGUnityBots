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
        entry.resourcePath = "/sequence/path";
        entry.filePath = "/sequence/path-to-delete";

        deleter.Initialize(entry);

        // ensure that we transfer the sequence entry's name and path to the script's display fields
        Assert.AreEqual(entry.sequenceName, deleter.sequenceNamePrefab.text);
        Assert.AreEqual(entry.resourcePath, deleter.resourcePath);
        Assert.AreEqual(entry.filePath, deleter.filePath);
    }

    [Test]
    public void OnDelete_HasSequencePath()
    {
        deleter.resourcePath = "/sequence/path";
        deleter.filePath = "/sequence/path-to-delete";

        deleter.OnDelete();

        // ensure that we reset the script's display fields after deleting
        Assert.IsEmpty(deleter.resourcePath);
        Assert.IsEmpty(deleter.filePath);
        Assert.IsEmpty(deleter.sequenceNamePrefab.text);
    }

    [Test]
    public void OnDelete_NoSequencePath()
    {

        deleter.resourcePath = null;
        deleter.filePath = null;

        // ensure that a missing sequence path throws an exception
        Assert.Throws<Exception>(() => deleter.OnDelete());
    }

    [Test]
    public void OnCancel()
    {
        // ensure that we can cancel the deletion and reset the scripts display fields
        Assert.DoesNotThrow(() => deleter.OnCancel());
        Assert.IsEmpty(deleter.resourcePath);
        Assert.IsEmpty(deleter.filePath);
        Assert.IsEmpty(deleter.sequenceNamePrefab.text);
    }
}
