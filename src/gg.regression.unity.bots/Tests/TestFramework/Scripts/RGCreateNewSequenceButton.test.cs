using NUnit.Framework;
using RegressionGames;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture]
public class RGCreateNewSequenceButtonTests
{
    private GameObject _uat;

    private RGCreateNewSequenceButton button;

    [SetUp]
    public void SetUp()
    {
        _uat = new GameObject();
        button = _uat.AddComponent<RGCreateNewSequenceButton>();
        button.transform.SetParent(_uat.transform, false);
        
        var overlay = new GameObject();
        var sequenceManager = overlay.gameObject.AddComponent<RGSequenceManager>();
        sequenceManager.sequenceEditor = new GameObject();

        button.overlayContainer = overlay;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(button);
        Object.Destroy(_uat);
    }

    [Test]
    public void OnClick()
    {
        button.OnClick();

        var sequenceManager = button.overlayContainer.GetComponent<RGSequenceManager>();
        Assert.IsTrue(sequenceManager.sequenceEditor.activeSelf);
    }
}