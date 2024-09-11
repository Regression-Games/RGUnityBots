using NUnit.Framework;
using RegressionGames;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture]
public class RGCancelSequenceEditButtonTests
{
    private GameObject _uat;

    private RGCancelSequenceEditButton button;

    [SetUp]
    public void SetUp()
    {
        // create the button we want to test
        _uat = new GameObject();
        button = _uat.AddComponent<RGCancelSequenceEditButton>();
        button.transform.SetParent(_uat.transform, false);
        
        // create the sequence manager the button references
        var overlay = new GameObject();
        var sequenceManager = overlay.gameObject.AddComponent<RGSequenceManager>();
        sequenceManager.sequenceEditor = new GameObject();

        button.overlayContainer = overlay;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_uat);
    }

    [Test]
    public void OnClick()
    {
        button.OnClick();

        // ensure that the sequence editor is closed on click
        var sequenceManager = button.overlayContainer.GetComponent<RGSequenceManager>();
        Assert.IsFalse(sequenceManager.sequenceEditor.activeSelf);
    }
}