using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture]
public class RGDraggedCardTests
{
    private GameObject _uat;

    private RGDraggedCard card;

    [SetUp]
    public void SetUp()
    {
        _uat = new GameObject();
        card = _uat.AddComponent<RGDraggedCard>();
        card.transform.SetParent(_uat.transform, false);
        card.draggedCardName = "Dragged Card";
        card.draggedCardDescription = "Dragged Description";
        card.payload = new Dictionary<string, string>();
        card.iconPrefab = new GameObject();
        card.namePrefab = RGTestUtils.CreateTMProPlaceholder();
        card.Start();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(card);
        Object.Destroy(_uat);
    }

    [Test]
    public void Initialize()
    {
        Assert.AreEqual(card.namePrefab.text, card.draggedCardName);
    }
}