# Action Manager Technical Reference

This document provides background and details about the Action Manager to enable developers to use and extend it for the purpose of creating generic bots that work with a wide range of games.

## Overview

The Action Manager automatically generates a model of game actions that can be used by bots to interact with the game. This is done through a combined static and dynamic analysis, where the game code and resources are analyzed to determine all valid keyboard, mouse, and other UI interactions, then during gameplay the current set of objects in the game scene are used to narrow down the applicable set of keyboard/mouse inputs for that frame. Furthermore, the user is given an opportunity through the Action Manager UI (under Regression Games -> Configure Bot Actions) to configure the set of enabled actions. This allows for disabling undesirable actions, such as Pause/Quit buttons.

The current main application of the Action Manager is the [Monkey Bot](https://docs.regression.gg/generic-bots/monkey-bot), which conducts a random test of the game using the precise set of keyboard/mouse actions determined by the Action Manager, and further allows the user to disable certain undesirable actions through the Action Manager UI.

## Action Analysis

Before the action manager can be used, a static analysis of the game actions must be conducted. To do this, the user opens the Action Manager panel by navigating to the **Regression Games > Configure Bot Actions** menu. The user then presses the **Analyze Actions** button to invoke the analysis, which inspects the game code and resources for all applicable keyboard/mouse inputs and other interactable UI elements.

![Action Manager Panel](https://github.com/Regression-Games/RegressionDocs/blob/9bba4d4faa06e47c529506c68da3a3b60d33d8d2/docs/generic-bots/img/action-manager-panel-final.png)

The result of the analysis produces a set of actions, each of which are associated with a component type. At this point the user can further configure the actions through the UI, for example disabling undesirable actions such as Pause/Quit buttons. The action analysis outcome is saved as a JSON resource in the game at `Assets/Resources/RGActionAnalysisResult.txt`, and the user's configuration is saved to `Assets/Resources/RGActionManagerSettings.txt`. Both of these can be commited to the game's repository and re-used by other developers/testers of the game.

During gameplay, when the bot requests the set of valid actions, an _instance_ of each action will be created for each active component in the scene that the action is associated with. This means that if the component is not present, the action will be considered invalid. 

The static analysis implementation is contained in the [RGActionAnalysis](../../../Editor/Scripts/ActionManager/RGActionAnalysis.cs) class. This is an analysis that associates keyboard/mouse input handlers found in the code (using the [Roslyn](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) code analysis SDK) with the components that listen for them and generates an [RGGameAction](RGGameAction.cs) for each identified action. It also iterates through all the scenes, game objects, and prefabs in the project to find Unity UI components and InputActionAssets (from the new Input System), generating appropriate actions for these as well.

A key design choice is that all the generated actions apply to a **single frame**. The final outcome of the action manager is the set of _immediate_ keyboard/mouse inputs that are applicable for a particular game frame (such as pressing/releasing keys, moving the mouse to a position, etc.).

The analysis currently supports the following types of actions:
1. Keyboard/mouse inputs handled via the Legacy Input Manager (`Input.GetAxis`, `Input.GetButton`, `Input.GetKey`, `Input.mousePosition`, etc.)
2. Keyboard/mouse inputs handled via the new Input System (`Keyboard.current`, `Mouse.current`)
3. Mouse input handled via MonoBehaviour callbacks (`OnMouseDown`, `OnMouseOver`, etc.)
4. Actions defined via InputActions and InputActionAssets in the new Input System
5. Unity UI (uGUI) interactable components (buttons, input fields, etc.)

## RGActionManager Usage

The [RGActionManager](RGActionManager.cs) class is the public interface to Action Manager that offers the following capabilities:
1. Iterating over the complete set of actions that were identified from the static analysis
2. Obtaining the subset of actions that are valid for the current game state
3. Determining and simulating the appropriate keyboard/mouse inputs needed to perform the actions

The following example code for a simple random exploration bot illustrates all of the above functionality:
```
public class RandomBot : MonoBehaviour, IRGBot
{
    private List<IRGGameActionInstance> validActions = new();
    
    void Start()
    {
        // Check that the action manager is available (if the user hasn't run the analysis, it won't be)
        if (!RGActionManager.IsAvailable)
        {
            Debug.LogError("Action manager unavailable");
            Destroy(this);
            return;
        }
        
        // Start an action manager session (must be called before using any of the action manager API)
        RGActionManager.StartSession(this);
        
        // Iterate over all the actions identified by the static analysis
        foreach (RGGameAction action in RGActionManager.Actions)
        {
            if (RGActionManager.IsActionEnabled(action))
            {
                Debug.Log("Action: " + action.DisplayName);
            }
        }
        
        DontDestroyOnLoad(this);
    }

    void Update()
    {
        validActions.Clear();
        
        // Request the set of valid action instances for this frame and store them in a buffer
        foreach (var actionInstance in RGActionManager.GetValidActions())
        {
            validActions.Add(actionInstance);
        }

        // Randomly select an action, generate a random parameter for it, and simulate the appropriate inputs
        var chosenAction = validActions[UnityEngine.Random.Range(0, validActions.Count)]; 
        var actionParam = chosenAction.BaseAction.ParameterRange.RandomSample();
        foreach (RGActionInput input in chosenAction.GetInputs(actionParam))
        {
            input.Perform();
        }
    }

    void OnDestroy()
    {
        // Stop the action manager session
        RGActionManager.StopSession();
    }
}
```

The above random bot implementation could be added in anywhere in the game's code and then deployed through the Regression Games Bot Manager overlay. The above example is limited in some ways: the action manager API allows for performing multiple actions at once (the above example only does one action at a time), and for some common actions such as mouse clicks it can improve performance to have heuristics that prioritize certain sequences of actions, such as pressing and releasing the mouse button over the same coordinate over two frames. An expanded version of this random bot can be found in the [RGMonkeyBot](../GenericBots/RGMonkeyBot.cs) implementation, which is built into the Regression Games SDK and is always available through the overlay.

## Extending The Action Manager



## Example Bots
