## RGValidations

Validations allow you to write code to validate the behaviour of your game as a sequence or segment completes.

![Example validations](example.png)

> Please note that this code is pretty experimental, but we will continue to improve this over the week as you provide
> feedback! If you do encounter any issues, let us know and we will jump right on it.

## RGValidate API

In order to get these validators running automatically, you create a class that inherits  
from `RGValidationScript` and then add the `RGValidate` attribute to any method you want  
to run as a validator. The `RGValidate` attribute has the following parameters:

- `ValidationMode`: The condition that this validator should meet. This can be `ALWAYS_TRUE`,  
  `EVENTUALLY_TRUE`, `NEVER_TRUE`, or `ONCE_TRUE_ALWAYS_TRUE`.
- `frequency`: How often this validator should run (i.e. number of frames). The default is every frame, but I would recommend setting this to something higher for performance reasons.

### Conditions

- `ValidationMode.ALWAYS_TRUE`: This validator should always assert true.
- `ValidationMode.EVENTUALLY_TRUE`: This validator should eventually assert true. It can fail to assert or assert false for any number of times, but must be true by the end of the run.
- `ValidationMode.NEVER_TRUE`: This validator should never assert true. It can fail to assert or assert false any number of times, but should never assert true.
- `ValidationMode.ONCE_TRUE_ALWAYS_TRUE`: This validator should assert true once, and then must always assert true after that. It can fail to assert or assert false before that.

Essentially, a validator can choose to do one of three things:
* Do nothing - this may be the case where you are waiting for an entity to appear. It is not false or true, but is internally marked as UNKNOWN.
* Assert true - this is the case where the condition being checked is now true. Note that this doesn't mean pass necessarily, because a NEVER_TRUE condition that asserts as true is technically a FAIL
* Assert false - this is the case where the condition being checked is now false. This is a FAIL.

### How it runs

A validation script will run until all end criteria and bot actions are finished in a segment or sequence. At that point,
any UNKNOWN validations will be marked as FAIL or PASS depending on the validation mode. All results get stored into a
JSON file in the same directory as the rest of your recording. You can find this by using our menu option:

```
Regression Games > Open Recordings Folder - and then open the folder for that recording, and open validations.json
```

Additionally, the results get printed to the console, and issue an error log if any of the validations fail. Eventually
we will show these results live in a window pane as well.

**Important**: These validations are run over multiple frames! This means that they aren't like unit tests where they
get evaluated once. The looping aspect of these tests allow you to store variables over time - for instance, a validation
test may set a class variable indicating a starting position, and then wait and check that the position moves. Here is
an example:

```csharp
public class ValidateMoved: RGValidationScript
{
    private float? previousXPosition = null;

    [RGValidate(ValidationMode.EVENTUALLY_TRUE, 100)] // This will run every 100 frames until it becomes true
    void PlayerDoesMove()
    {
        
        var player = GameObject.Find("Player");
        if (player == null) return; // No player found, just return early and wait
        
        var playerPosition = player.transform.position.x;
        
        // If this is the first time we are seeing the player, just store their original position
        if (previousXPosition == null) {
            previousXPosition = player.transform.position.x;
        } else {
            // If the player has moved at least 10 units, assert true
            if (Math.Abs(playerPosition - previousXPosition.Value) > 10) {
                AssertAsTrue("Player has moved");
            }
        }
        
    }
}
```

Note that any variable you store in the class should be reset in this `ResetValidationStates` method. This method is
used when a validation is re-run, so it's important to reset any variables that you are using to track state (see the
example at the bottom of this page).

### Adding it to your sequences

You can add validations to either segments or sequences by adding a new `validations` field that looks like the following.
Note that `classFullName` is the full classpath of the C# file you want to use, and `type` for now is just `Script`. This
also works in bot segment files if you want to make a validation that only runs for one segment.

```json
{
"name": "My Sequence",
"description": "",
"segments":[
  {"apiVersion":24,"path":"Assets/RegressionGames/Resources/BotSegments/SomeSegments/1.json"},
  {"apiVersion":24,"path":"Assets/RegressionGames/Resources/BotSegments/SomeSegments/2.json"},
  {"apiVersion":24,"path":"Assets/RegressionGames/Resources/BotSegments/SomeSegments/3.json"}
],
  "validations": [
    {
      "type": "Script",
      "data": {
        "classFullName": "Unity.Multiplayer.Samples.BossRoom.RegressionGames.ValidateUI"
      }
    }
  ]
}
```

### Example Validation File

Here you can see an example of some validations for a menu screen in our BossRoom sample. You'll notice that you can
store variables and use them across 

```csharp
using System;
using RegressionGames.Validation;
using StateRecorder.BotSegments.Models.SegmentValidations;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Multiplayer.Samples.BossRoom.RegressionGames
{
    public class ValidateUI: RGValidationScript
    {
        private bool profileDialogAppeared = false;
        private float previousParticleTime = float.NegativeInfinity;

        public override void ResetValidationStates()
        {
            base.ResetValidationStates();
            profileDialogAppeared = false;
            previousParticleTime = float.NegativeInfinity;
        }

        [RGValidate(ValidationMode.EVENTUALLY_TRUE, 100)]
        void ProfileButtonAppears()
        {
            var profileButton = GameObject.Find("Profile Button");
            if (profileButton)
            {
                AssertAsTrue("Profile button has appeared");
            }
        }

        [RGValidate(ValidationMode.EVENTUALLY_TRUE, 100)]
        void ChangeProfileDialogAppears()
        {
            var profileButton = GameObject.Find("ProfilePopup");
            if (profileButton)
            {
                var canvasGroup = profileButton.GetComponent<CanvasGroup>();
                if (canvasGroup.alpha > 0)
                {
                    profileDialogAppeared = true;
                    AssertAsTrue("Profile dialog has appeared");
                }
            }
        }
        
        [RGValidate(ValidationMode.EVENTUALLY_TRUE, 100)]
        void RGButtonToBeInteractableAfterProfileDialog()
        {
            var startButton = GameObject.Find("RG Start Button");
            if (startButton)
            {
                var button = startButton.GetComponent<Button>();
                if (button.interactable && profileDialogAppeared)
                {
                    AssertAsTrue("RG Button is selectable after profile dialog has appeared");
                }
                else if (button.interactable && !profileDialogAppeared)
                {
                    AssertAsFalse("Profile dialog has not appeared yet, but RG button is selectable");
                }
            }
        }
        
        [RGValidate(ValidationMode.ONCE_TRUE_ALWAYS_TRUE, 10)]
        void FireAnimationShouldBePlaying()
        {
            var torchFire = GameObject.Find("FX_Torch_Fire/Fire");
            if (torchFire)
            {
                var fireParticleSystem = torchFire.GetComponentInChildren<ParticleSystem>();
                var currentAnimationTime = fireParticleSystem.time;
                
                // Ensure that the fire animation is playing
                if (Math.Abs(currentAnimationTime - previousParticleTime) < 0.01f)
                {
                    AssertAsFalse("Fire animation is not playing");
                }
                else
                {
                    AssertAsTrue("Fire animation is playing");
                }
            }
        }
        
    }
}

```

