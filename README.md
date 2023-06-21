# Regression Games Unity Bots

### What is Regression Games Unity Bots
Regression Games Unity Bots work by connecting Regression Games services to your game via a TCP socket connection.  Your game will use the Regression Games Unity Bots toolkit to send your game's 'State' to your bot running in Regression Games.  Your bot will interperet the state and send back a list of Action(s) for the bot to perform in your game.

### What is included in this package

- **Regression Games Unity Bots Replay Window**
  - This window allows replaying bot runs in your Unity editor so you can visualize the objects in the scene and their actions
  - *Note: Using this window will dynamically add assets to the current scene to help visualize the replay.  These assets will automatically be removed on Play, Build, scene save, etc so you should not need to worry about managing them.*

- **Regression Games Unity Bots In Game Overlay**
  - Allows starting and stopping Regression Games Bots to test your game.  This component includes the behaviours for managing the socket connections to/from Regression Games.
  
- **Regression Games Unity Bots toolkit**
  - Allows integration of your game with Regression Games to simulate players, test scenarios, or other bot driven scenarios.

- **Regression Games Unity Project Settings**
  - *Edit/Project Settings/Regression Games*
  - Configure your username/password for connecting to Regression Games servers
  - Enable/Disable the visiblity of the Regression Games In Game Overlay
  - Enable/Configure bots that load as match players for your game

### Installing the Package

1. Open your game project in the Unity Editor
2. Open the Window -> Package Manager Menu
3. Add the Regression Games Unity Bots package to your project
    - *Note: This package utilizes TextMeshPro.  If you are prompted to add TextMeshPro assets to your project by Unity, please add them.*

### Integrating Regression Games Unity Bots into your game

#### Adding the Regression Games overlay (REQUIRED)
  - To add the Regression Games overlay to your project, add the `Regression Games Unity Bots/Runtime/Prefabs/RGOverlayCanvas.prefab` to your existing scene(s).
    - This object will stay active across scenes, so you should normally add this to your first main menu scene.
  - **Note: This object must be added for Regression Games Unity Bots integration to work.  Its in game visiblity can be hidden using the Regression Games Unity project settings.**
  - If you want to show and actively use the overlay, ensure that your scene(s) have an EventSystem.  Without an EventSystem the overlay will not be clickable.

#### Representing the 'State' of your game to Regression Games Bots
- Any GameObject in a Scene that you want to appear in the 'State' must have 1 [RGAgent](Runtime/Scripts/RGBotConfigs/RGAgent.cs) and 1 or more implementations of [RGState](Runtime/Scripts/RGBotConfigs/RGState.cs) attached to it
- The RGAgent behaviour registers the GameObject as something tracked by the Regression Games Unity toolkit for sending states and processing actions.
- The RGState behaviour implementation identifies the important field(s) on the object that should be represented as part fo the GameObject's state.  Normal examples are position, rotation, player health, ability cooldowns, button states, switch states, rigidbody attibutes (velocity, mass, etc), what team a player is on, or any other actionable attribute that may be unique to your game or that GameObject.
- Set the `objectType` field on the RGState beahviour to a unique identifying name for that GameObject.  This type will be used by your bot code to help identify the gameObject(s).  If you are putting RGState on a Prefab GameObject, then the type will be the same on all instances of that Prefab in the scene.
- **TODO: Add screenshot showing RGPlayerState on a GameObject**
- Sample RGState implementation for representing a player in Unity Boss Room [RGPlayerState](https://github.com/Regression-Games/RGBossRoom/blob/main/Assets/Scripts/Gameplay/RegressionGames/RGBossRoom/RGPlayerState.cs)

#### Performing 'Actions' using Regression Games Bots
- Any GameObject in a Scene that can perform actions must have 1 or more implementations of [RGAction](Runtime/Scripts/RGBotConfigs/RGAction.cs) attached
- When a list of actions is returned from a Regression Games bot, the registered action(s) that match that command type will be performed on the next Unity Update call.
- **TODO: Add screenshot showing RGAction on a GameObject**
- Sample RGAction implementation for performing a player skill in Unity Boss Room [RGPerformSkillAction](https://github.com/Regression-Games/RGBossRoom/blob/main/Assets/Scripts/Gameplay/RegressionGames/RGBossRoom/RGPerformSkillAction.cs)

#### Spawning Bot characters/avatars as part of a match in your Game (OPTIONAL)
1. Create an implementation of the [RGBotSpawnManager](Runtime/Scripts/Scripts/RGBotSpawnManager.cs) interface 
    - For example, see the implementation for Unity Boss Room [RGBossRoomBotSpawnManager](https://github.com/Regression-Games/RGBossRoom/blob/main/Assets/Scripts/Gameplay/RegressionGames/RGBossRoom/RGBossRoomBotSpawnManager.cs)
    - This script will manage the integration with your game for seating players, spawning players, and tearing them back down at the end of a match.
2. Add the RGBotSpawnManager implemenation behaviour script to a GameObject in all scene(s) where playes can be connected to your game for a match.  This normally includes lobbies, matchmaking, gameplay, and postmatch summary scenes.
    - *See `Regression Games Unity Bots/Samples/BossRoom/RGBossRoomBotSpawnManager.cs.sample for an example of seating and spawning a player in Unity's BossRoom multiplayer demo game*
3. Add code hooks into your game to signal Regression Games when to start the bots.
```csharp 
RGSettings rgSettings = RGSettings.GetOrCreateSettings();
if (rgSettings.GetUseSystemSettings())
{
  int[] botIds = rgSettings.GetBotsSelected().ToArray();
  int errorCount = 0;
  if (botIds.Length > 0)
  {
    Task.WhenAll(botIds.Select(botId =>
      RGServiceManager.GetInstance()?.QueueInstantBot((long)botId, (botInstance) => { }, () => errorCount++))
    );
  }
  if (errorCount > 0)
  {
    Debug.Log($"Error starting {errorCount} of {botIds.Length} RG bots, starting without them");
  }
}
```
4. Add code hooks into your game to signal Regression Games when to stop the bots.
```csharp 
RGBotServerListener.GetInstance()?.StopGame();
```

### Writing a Regression Games Bot (Javascript)
Regression Games Unity Bots are written in Javascript using the Regression Games Bot APIs.  The bots run in Regression Games runtime and connect to your game via a socket connection initiated from your game's integration to Regression Games.

#### Regression Games Bot Lifecycle
Regression Games bot lifecycles can be managed a few ways.  You can use your integration to automatically start bots for a match to simulate players, or you can start bots to simulate humans clicking buttons and playing the game, or you can manually start bots to represent players in the game.

When you start a bot, your game integration makes a request to Regression Games to start a specific bot implemention.  When the bot starts, it connects back to your game via TCP socket using the address and security token supplied by your game in the request.  Once the bot's initial handshake completes, the Regression Games Unity bot toolkit will start sending it the 'state' of the game every tick until the bot completes or disconnects.

#### Regression Games Bot Javascript API
Each bot implementation should implement the following methods.

```javascript
/**
 * Returns One of ...
 * MANAGED - Server disconnects/ends bot on match/game-scene teardown
 * PERSISTENT - Bot is responsible for disconnecting / ending itself
 */
getBotLifeCycle() {
  return 'MANAGED'
}
```

```javascript
/**
 * If the bot reports that it is complete when processing a tick, it will stop processing and send a teardown notification to your game for this specific bot.
 * This is most useful for testing bots that perform a specific set of steps and/or validations and then are 'complete'.
 */
isComplete() {
  return false
}
```

```javascript
/**
 * Specifies whether this bot supports a character in the main game.
 * default to true (menu bots would potentially be false if they don't also drive a player avatar in game)
 */
isSpawnable() {
  return true
}
```

```javascript
/**
 * Provides the character type string for the initial handshake.  This helps your game seat your player in the
 * appropriate seat or choose the correct player class to play as.  This is a freeform string to be interpereted by your
 * game's implementation of the RGBotSpawnManager.SeatPlayer and RGBotSpawnManager.SpawnBot interfaces.
 */
getCharacterType() {
  return "Mage"
}
```

```javascript
/**
 * The place where all the magic happens.  This is the primary processing method for your bot which will always be called once per tick.
 * @param playerId - the numeric identifier for your bot within the tickInfo data.  This normally maps to the transform id for your bot when it represents a playable avatar
 * @param tickInfo - the json representing the tick info for your game at the current tick
 * @param matchInfo - DEPRECATED - To be removed
 * @param actionQueue - The queue to which you can append new actions to be performed based on your bot's processing of this tick.
 */
runTurn(playerId, tickInfo, matchInfo, actionQueue) {
  // Implement tick processing here
  // Refer to the sample bots for examples of accessing state from the tickInfo or adding actions to the actionQueue
}
```

#### Sample Bots
- Sample bot for Unity BossRoom that picks a random character and cycles to a new ability action on each tick... [AbilityBot](https://github.com/Regression-Games/UnityTestBot/blob/main/abilitybot/index.js)
- Sample bot for Unity BossRoom that follows another player around and supports them... [CoopBot](https://github.com/Regression-Games/UnityTestBot/blob/main/coopbot/index.js)
- Sample bot for Unity BossRoom that clicks menu buttons to start the game without human intervention... [MenuBot](https://github.com/Regression-Games/UnityTestBot/blob/main/menubot/index.js)

### Using the Regression Games Bot Replay Unity tool to visualize a Bot's replay
The Regression Games Unity Bot Replay window is a helpful tool for visualizing what your bot(s) did in a previous test run or gameplay session.  For each 'tick' of the game, Regression Games saves both the state and the actions that the bot took.  This replay file can be opened in the Replay window to visualize the positions, targets, actions, cooldowns, etc for every tracked GameObject in your scene.  The ticks can then be "played" or stepped through to visualize the state of the game at every recorded tick interval.  You can also highlight specific GameObjects in the replay, show their pathing history, their targeting, and/or their actions per tick overlayed into the scene itself.
- You can also define custom replay models for each of your RGState types.  Without custom models, all GameObjects will be represented as a default capsule model during the replay.
  - Update `Regression Games Unity Bots/Editor/Prefabs/RGReplayObject.Object.Model.ReplayModelManager(behaviour) to add character type name to Prefab mappings for each GameObject that is implemented with RGState.
    - (TODO: This is a bad pattern. We will update this in the near future to be cleaner/easier and not involve changing a Regression Games prefab!).
- TODO: This tool currently works well for scenes where all the assets are pre-placed and have a well defined position.  For heavily procedurally generated scenes (ex: Diablo like dungeon crawler), more of the scene elements need to be represented in the state for the replay to provide the required level of detail.
- TODO/FUTURE: This tool has a space reserved for showing validations performed by bots.  These are not currently processed from the replay data.
- **TODO: Add screenshots of the replay window and a scene replay with pathing/targeting/etc for BossRoom**
