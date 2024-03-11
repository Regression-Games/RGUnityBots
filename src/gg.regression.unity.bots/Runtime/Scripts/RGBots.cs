using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace RegressionGames
{
    public static class RGBots
    {
        private static IRGBotEntry FindBot(string botName)
        {
            var botList = Resources.Load<IRGBotList>("RGBots");

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
            var botList = Resources.Load<IRGBotList>("RGBots");

            var botEntry = botList.botEntries.FirstOrDefault(b => b.qualifiedName == assemblyQualifiedName);
            if (botEntry != null)
            {
                return botEntry;
            }

            return null;
        }

        /// <summary>
        /// Creates a new bot, with the typed behavior T. Adds an empty GameObject to the
        /// current scene, then adds the bot script component 
        /// </summary>
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

        /// <summary>
        /// Creates a new bot, with the typed behavior T. Instantiates the given gameObject
        /// in the current scene, then adds the bot script component 
        /// </summary>
        public static GameObject SpawnBot<T>(GameObject prefab) where T : Component
        {
            return SpawnBot<T>(prefab, Vector3.zero);
        }

        /// <summary>
        /// Creates a new bot, with the typed behavior T. Instantiates the given gameObject
        /// in the current scene, sets the given position, then adds the bot script component 
        /// </summary>
        public static GameObject SpawnBot<T>(GameObject prefab, Vector3 position) where T : Component
        {
            GameObject botGameObject = GameObject.Instantiate(prefab);
            botGameObject.transform.position = position;
            var botEntry = FindBotByType<T>();
            var behaviorName = botEntry?.botName ?? "Empty";
            if (botEntry != null)
            {
                botGameObject.AddComponent<T>();
            }

            LinkToOverlay(botGameObject, behaviorName);
            return botGameObject;
        }

        /// <summary>
        /// Creates a new bot, with the behavior of the given name. Instantiates an empty gameObject
        /// in the current scene, finds the behavior matching the name, then adds the bot script
        /// component 
        /// </summary>
        public static GameObject SpawnBot(string botName)
        {
            GameObject botGameObject = new GameObject(botName);
            var botEntry = FindBot(botName);
            var behaviorName = botEntry?.botName ?? "Empty";
            if (botEntry != null)
            {
                botGameObject.AddComponent(System.Type.GetType(botEntry.qualifiedName));
            }

            LinkToOverlay(botGameObject, behaviorName);
            return botGameObject;
        }

        /// <summary>
        /// Creates a new bot, with the behavior of the given name. Instantiates the given
        /// gameObject in the current scene, finds the behavior matching the name, then adds
        /// the bot script component 
        /// </summary>
        public static GameObject SpawnBot(string botName, GameObject prefab)
        {
            return SpawnBot(botName, prefab, Vector3.zero);
        }

        /// <summary>
        /// Creates a new bot, with the behavior of the given name. Instantiates the given
        /// gameObject in the current scene, sets the given position, finds the behavior
        /// matching the name, then adds the bot script component 
        /// </summary>
        public static GameObject SpawnBot(string botName, GameObject prefab, Vector3 position)
        {
            GameObject botGameObject = GameObject.Instantiate(prefab);
            botGameObject.transform.position = position;
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
}
