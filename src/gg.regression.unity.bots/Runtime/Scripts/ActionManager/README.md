# Action Manager Technical Reference

This document provides a technical reference for the Action Manager to enable developers to use and extend it for the purpose of creating generic bots that work with a wide variety of games.

## Overview

The Action Manager automatically generates a model of game actions that can be used by bots to interact with the game. This is done through a combined static and dynamic analysis, where the game code and resources are analyzed to determine all valid keyboard, mouse, and other UI interactions. During gameplay, the active set of objects/components in the game scene are used to narrow down the applicable set of keyboard and mouse inputs for that frame. Furthermore, the user is given an opportunity through the Action Manager UI (under **Regression Games > Configure Bot Actions**) to configure the set of enabled actions. This allows for disabling undesirable actions, such as Pause/Quit buttons.

The current main application of the Action Manager is the [Monkey Bot](https://docs.regression.gg/generic-bots/monkey-bot), which conducts a random test of the game using the precise set of keyboard/mouse actions determined by the Action Manager, and further allows the user to disable certain undesirable actions through the Action Manager UI.

## Action Analysis

Before the action manager can be used, a static analysis of the game actions must be performed. To do this, the user opens the Action Manager panel by navigating to the **Regression Games > Configure Bot Actions** menu. The user then presses the **Analyze Actions** button to invoke the analysis, which inspects the game code and resources for all applicable keyboard/mouse inputs and other interactable UI elements.

![Action Manager Panel](https://github.com/Regression-Games/RegressionDocs/blob/9bba4d4faa06e47c529506c68da3a3b60d33d8d2/docs/generic-bots/img/action-manager-panel-final.png)

The result of the analysis produces a set of actions, each of which are associated with a component type. For example, it could recognize that a MonoBehaviour listens for a certain keyboard input and generate an action that associates the keyboard input with the component. At this point the user can further configure the actions through the UI, such as by disabling undesirable actions like Pause/Quit buttons. The action analysis outcome is saved as a JSON resource in the game at `Assets/Resources/RGActionAnalysisResult.txt`, and the user's configuration is saved to `Assets/Resources/RGActionManagerSettings.txt`. Both of these can be commited to the game's repository and re-used by other developers/testers of the game.

The static analysis implementation is contained in the [RGActionAnalysis](../../../Editor/Scripts/ActionManager/RGActionAnalysis.cs) class. This is an analysis that associates keyboard/mouse input handlers found in the code (using the [Roslyn](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) code analysis SDK) with the components that listen for them and generates an [RGGameAction](RGGameAction.cs) for each identified action. It also iterates through all the scenes, game objects, and prefabs in the project to find Unity UI components and InputActionAssets (from the new Input System), generating appropriate actions for these as well.

A key design choice is that all the identified actions apply to a **single frame**. The final outcome of the action manager is the set of _immediate_ keyboard/mouse inputs that are applicable for the current game frame (such as pressing/releasing keys, moving the mouse to a position, etc.).

The analysis currently supports the following types of actions:
1. Keyboard/mouse inputs handled via the Legacy Input Manager (`Input.GetAxis`, `Input.GetButton`, `Input.GetKey`, `Input.mousePosition`, etc.)
2. Keyboard/mouse inputs handled via the new Input System (`Keyboard.current`, `Mouse.current`)
3. Mouse input handled via MonoBehaviour callbacks (`OnMouseDown`, `OnMouseOver`, etc.)
4. Keyboard/mouse actions defined via InputActions and InputActionAssets in the new Input System
5. Unity UI (uGUI) interactable components (buttons, input fields, etc.)

During gameplay, the bot invokes the `RGActionManager.GetValidActions()` method to request the set of valid actions, which is a subset of the full set of identified actions. An _instance_ of each action will be created for each active component in the scene that the action is associated with. This means that if the component is not present, the action will be considered invalid since it does not have any instances.

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
        
        // Iterate over all the enabled actions
        foreach (RGGameAction action in RGActionManager.Actions)
        {
            Debug.Log("Action: " + action.DisplayName);
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

        // Randomly select an action and a random parameter for it, then simulate the appropriate inputs
        var chosenAction = validActions[UnityEngine.Random.Range(0, validActions.Count)]; 
        var actionParam = chosenAction.BaseAction.ParameterRange.RandomSample();
	if (chosenAction.IsValidParameter(actionParam))
	{
		foreach (RGActionInput input in chosenAction.GetInputs(actionParam))
		{
			input.Perform();
		}
	}
    }

    void OnDestroy()
    {
        // Stop the action manager session
        RGActionManager.StopSession();
    }
}
```

The above example can be added to the game project and then deployed through the Regression Games Bot Manager overlay. The example is limited in various ways. For one, the action manager API allows for performing multiple actions at once, while the above example only does one action at a time. For some common actions such as mouse clicks it can improve performance to have heuristics that prioritize certain sequences of actions, such as pressing and releasing the mouse button over the same coordinate over two frames. Also, the `IsValidParameter` method could potentially return false, which could warrant making another attempt at randomly sampling the parameter space. An expanded version of this random bot can be found in the [RGMonkeyBot](../GenericBots/RGMonkeyBot.cs) implementation, which is built into the Regression Games SDK and is always available through the overlay.

## Extending The Action Manager

To introduce support for a new action type in the action manager:
1. Define the new action type in the [Actions](Actions) directory, following the pattern established by the existing actions.
2. Add analysis code to [RGActionAnalysis](../../../Editor/Scripts/ActionManager/RGActionAnalysis.cs) that matches the appropriate code/resource pattern and calls the `AddAction` method to add the action to the analysis results.
3. Add a Play Mode test case to [RGActionManagerTests.cs](../../../../RGUnityBots/Assets/Tests/Runtime/RGActionManagerTests.cs) that verifies that the action's validity and inputs are correctly determined.

## Example Bots

[RGMonkeyBot](../GenericBots/RGMonkeyBot.cs) - Random testing bot that performs actions from the action manager and also includes some heuristics to prioritize some common sequences of actions (such as pressing down and releasing a mouse button over the same coordinate over the course of two frames)
[QLearningBot](../GenericBots/Experimental/QLearningBot.cs) - Exploratory bot based on Q learning that generates a discrete action space for the game using the action manager, and by default uses a reward function that tries to maximize the number of camera positions visited
