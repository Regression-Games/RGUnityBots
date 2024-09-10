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
        _uat = new GameObject();
        entry = _uat.AddComponent<RGSequenceEntry>();
        entry.sequenceName = "Sequence Entry Name";
        entry.description = "Sequence Entry Description";
        entry.playAction = () => { };
        entry.playButton = entry.gameObject.AddComponent<Button>();
        entry.nameComponent = RGTestUtils.CreateTMProPlaceholder();
        entry.descriptionComponent = RGTestUtils.CreateTMProPlaceholder();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(entry);
        Object.Destroy(_uat);
    }

    [Test]
    public void Start()
    {
        entry.Start();

        Assert.AreEqual(entry.sequenceName, entry.nameComponent.text);
        Assert.AreEqual(entry.description, entry.descriptionComponent.text);
    }
    
    [Test]
    public void OnPlay()
    {
        var playActionCalled = false;
        entry.playAction = () =>
        {
            playActionCalled = true;
        };
        
        entry.OnPlay();

        Assert.IsTrue(playActionCalled);
    }
}