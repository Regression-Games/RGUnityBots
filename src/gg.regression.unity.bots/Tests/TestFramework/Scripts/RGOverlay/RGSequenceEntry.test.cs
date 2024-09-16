using NUnit.Framework;
using RegressionGames;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class RGSequenceEntryTests
{
    private GameObject _uat;

    private RGSequenceEntry entry;
    
    [SetUp]
    public void SetUp()
    {
        // create the entry we want to test
        _uat = new GameObject();
        entry = _uat.AddComponent<RGSequenceEntry>();
        entry.sequenceName = "Sequence Entry Name";
        entry.description = "Sequence Entry Description";
        entry.playButton = entry.gameObject.AddComponent<Button>();
        entry.nameComponent = RGTestUtils.CreateTMProPlaceholder();
        entry.descriptionComponent = RGTestUtils.CreateTMProPlaceholder();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_uat);
    }

    [Test]
    public void Start()
    {
        entry.Start();

        // ensure that the entry consumes its public fields properly
        Assert.AreEqual(entry.sequenceName, entry.nameComponent.text);
        Assert.AreEqual(entry.description, entry.descriptionComponent.text);
    }
    
    [Test]
    public void OnPlay()
    {
        // mock the action that the play button uses
        var playActionCalled = false;
        entry.playAction = () =>
        {
            playActionCalled = true;
        };
        
        entry.OnPlay();

        // ensure that the play button's mock action was utilized
        Assert.IsTrue(playActionCalled);
    }
}