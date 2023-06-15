This is the readme for how to integrate the Regression Games Unity Bots Package into your project.


Installing the Package

1. Open your project in the Unity Editor
2. Open the Window -> Package Manager Window
3. Add the Regression Games Uinty Bots package to your project
  - Note: This package utilizes TextMeshPro.  If you are prompted to add TextMeshPro assets to your project by Unity, please add them.



What is included in this package ?

- Regression Games Bot Replay Window
  - This window allows replaying bot runs in your Unity editor so you can visualize the objects in the scene and their actions
  - Note: Using this window will automatically add assets to the current scene to help visualize the replay.  These assets will automatically be removed on Play, Build, scene save, etc so you should not need to worry about managing them.

- Regression Games In Game Overlay
  - Allows starting and stopping Regression Games Bots to test your game
  - To add this to your project, add the Runtime/Prefabs/RGOverlayCanvas.prefab to your existing scene(s).  This object will stay active across scenes, so you should normally add this to your first main menu scene.  Note that this object must be added for Regression Games integration to work.  Its visiblity can be hidden using the project settings.
    - Make sure your scene has an EventSystem.  Without it the overlay will not be clickable.

- Regression Games Unity Project Settings
  - Edit/Project Settings/Regression Games
  - Configure your username/password for connecting to regression games
  - Optionally enable bots that auto load as match players for your game
  - Enable/Disable the Regression Games overlay being visible



How to integrate the package with your game
TODO: Write these with code examples
- RGState on GameObjects
- Actions on GameObjects
- Defining custom replay models based on your RGState types.  If you don't, all models will be a default capsule model
  - Update Editor/Prefabs/RGReplayObject.Object.Model.ReplayModelManager(script) to add character type name to Prefab mappings for each GameObject that is implemented with RGState. (TODO: Maybe in the future we update this to be part of the RGState or RGAgent itself?). 
- Starting Regression Games Bots on match start
  ``` 
RGSettings rgSettings = RGSettings.GetOrCreateSettings();
if (rgSettings.GetUseSystemSettings())
{
    int[] botIds = rgSettings.GetBotsSelected().ToArray();
    int errorCount = 0;
    Task.WhenAll(botIds.Select(botId =>
        RGServiceManager.GetInstance()
            ?.QueueInstantBot((long)botId, (botInstance) => { }, () => errorCount++)));
    if (errorCount > 0)
    {
        Debug.Log($"Error starting {errorCount} of {theBotCount} RG bots, starting without them");
    }
}
  ```
- Spawning Bot characters/avatars as part of a match in your Game
  - Create an implementation of the interface RGBotSpawnManager
  - Add a GameObject to all scene(s) where playes can be connected to your game for a match.  This includes lobbies, matchmaking, and actual gameplay scenes.
  - See samples/BossRoom/RGBossRoomBotSpawnManager.cs.sample for an example of seating and spawning a player in Unity's BossRoom multiplayer demo game 


Writing a Regression Games Bot
- TODO: Fill in this section about interpreting state, sending actions, adding validations
- TODO: Sequence Diagram for regression games bot lifecycle


Sample Bots
TODO: Provide these samples + project to use them in and/or context
- Link to sample player bot with actions
- Link to Sample of Button pusing bot


Using the Regression Games Bot Replay Window
TODO: Write this section
- TODO: Add validations to the payload and put them into the replay