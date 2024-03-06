using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using UnityEngine;

public static class RGBots
{
    private static IRGBotEntry FindBot(string botName)
    {
        var botList = Resources.Load<IRGBotList>("RGBotList");

        var botEntry = botList.botEntries.FirstOrDefault(b => b.botName == botName);
        if (botEntry != null)
        {
            return botEntry;
        }
        return null;
    }

    private static IRGBotEntry FindBotByType<T>() where T : Component
    {
        var assemblyQualifiedName = typeof(T).AssemblyQualifiedName;
        var botList = Resources.Load<IRGBotList>("RGBotList");

        var botEntry = botList.botEntries.FirstOrDefault(b => b.qualifiedName == assemblyQualifiedName);
        if (botEntry != null)
        {
            return botEntry;
        }
        return null;
    }

    public static GameObject SpawnBot<T>() where T : Component
    {
        GameObject botGameObject = new GameObject(typeof(T).Name);
        var botEntry = FindBotByType<T>();
        var behaviorName = botEntry?.botName ?? "Empty";
        if (botEntry != null)
        {
            botGameObject.AddComponent<T>();
        }
        LinkToOverlay(botGameObject, behaviorName);
        return botGameObject;
    }

    public static GameObject SpawnBot<T>(GameObject prefab) where T : Component
    {
        GameObject botGameObject = GameObject.Instantiate(prefab);
        var botEntry = FindBotByType<T>();
        var behaviorName = botEntry?.botName ?? "Empty";
        if (botEntry != null)
        {
            botGameObject.AddComponent<T>();
        }
        LinkToOverlay(botGameObject, behaviorName);
        return botGameObject;
    }

    public static GameObject SpawnBot(string botName)
    {
        GameObject botGameObject = new GameObject();
        var botEntry = FindBot(botName);
        var behaviorName = botEntry?.botName ?? "Empty";
        if (botEntry != null)
        {
            botGameObject.AddComponent(System.Type.GetType(botEntry.qualifiedName));
        }
        LinkToOverlay(botGameObject, behaviorName);
        return botGameObject;
    }

    public static GameObject SpawnBot(string botName, GameObject prefab)
    {
        GameObject botGameObject = GameObject.Instantiate(prefab);
        var botEntry = FindBot(botName);
        var behaviorName = botEntry?.botName ?? "Empty";
        if (botEntry != null)
        {
            botGameObject.AddComponent(System.Type.GetType(botEntry.qualifiedName));
        }
        LinkToOverlay(botGameObject, behaviorName);
        return botGameObject;
    }

    private static void LinkToOverlay(GameObject runtimeObject, string botName)
    {
        var botManager = GameObject.FindObjectOfType<RGBotManager>();
        if (botManager == null)
        {
            return;
        }
        botManager.AddActiveBot(runtimeObject, botName);
    }
}
