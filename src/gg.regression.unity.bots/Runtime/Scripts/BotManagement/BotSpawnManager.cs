using System;
using System.Collections;
using System.Collections.Generic;
using RegressionGames;
using UnityEngine;
using RegressionGames;
using RegressionGames.Types;

public class BotSpawnManager : RGBotSpawnManager
{
    [SerializeField]
    private GameObject rgBotPrefab;

    protected override void Awake()
    {
        base.Awake();

        // Prepares the RG SDK for spawning bots
        RGBotServerListener.GetInstance()?.StartGame();
        RGBotServerListener.GetInstance()?.SpawnBots();

    }

    private void Start()
    {

    }

    private void OnDestroy()
    {
        // Stops all bots and cleans up the RG SDK
        RGBotServerListener.GetInstance()?.StopGame();
    }

    public override GameObject SpawnBot(bool lateJoin, BotInformation botInformation)
    {
        // Loads the bot prefab and spawns it into the scene
        var bot = Instantiate(rgBotPrefab, Vector3.zero, Quaternion.identity);
        return bot;
    }
}
