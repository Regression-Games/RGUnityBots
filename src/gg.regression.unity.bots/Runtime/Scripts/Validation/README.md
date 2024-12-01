# Prototype - Code-based Validations

This partial feature is meant to convey some ideas around code-based validations. I wanted
to explore how easy it is to write validations for a bot sequence within code, and while
there are more features I would want to implement here, I wanted to share this with the team
and get some feedback.

## Overall Idea

The overall idea here is to allow developers to write simple functions that act as
validators for a sequence. For instance, you may want to validate that:

- A certain UI element appears
- The animation of some particle system is playing correctly
- That a certain relationship between entities is present

The primary goal here is **simplicity**, making sure it feels easy and straightforward to
write these rules. I therefore created a system that felt very similar to Unit tests, but
with some additional pieces to account for the fact that these validators run often (due to
the update loop).

**NOTE TO REVIEWERS:** I hope to get across these main points with this system, to clarify further from
our conversation in the validators Slack channel where there were some doubts with this system.

1. These are not thought of as "writing a MonoBehaviour". There is no concept of a MonoBehaviour exposed here.
2. I provide some simple test examples here, but the goal is to use this for situations where simple relationships are
   easy to write in code, but difficult in no-code ways. See the pots example for a situation where the code is easy,
   but getting that exact logic in no-code would be tricky.

The resulting prototype is an `RGValidate` attribute that says "this method is a validator, it runs
at a certain frequency, and we expect it to be either always true, eventually true, or never
true". These are automatically evaluated and are shown in a pane in the editor.

Here is a quick example:

```csharp
[RGValidate(RGCondition.EVENTUALLY_TRUE, 100)]
void ProfileButtonAppears()
{
    var profileButton = GameObject.Find("Profile Button");
    if (profileButton)
    {
        AssertAsTrue("Profile button has appeared");
    }
}
```

Here is what this does:

1. Run this function every 100 frames (the default is every frame)
2. This function should "assert true" at some point. If it doesn't, the sequence will fail.
3. Once this is marked as true, this function will no longer run.

Once validations are passing, failing, or waiting, it will show directly in the editor.
Imagine that this would be tied to whatever system we use for tracking results.

![Validation Example](demo.png)

## RGValidate API

In order to get these validators running automatically, you create a class that inherits
from `RGValidateBehaviour` and then add the `RGValidate` attribute to any method you want
to run as a validator. The `RGValidate` attribute has the following parameters:

- `condition`: The condition that this validator should meet. This can be `ALWAYS_TRUE`,
  `EVENTUALLY_TRUE`, `NEVER_TRUE`, or `ONCE_TRUE_ALWAYS_TRUE`.
- `frequency`: How often this validator should run (i.e. number of frames). The default is every frame, but I would recommend setting this to something higher for performance reasons.

### Conditions

- `RGCondition.ALWAYS_TRUE`: This validator should always assert true.
- `RGCondition.EVENTUALLY_TRUE`: This validator should eventually assert true. It can fail to assert or assert false for any number of times, but must be true by the end of the run.
- `RGCondition.NEVER_TRUE`: This validator should never assert true. It can fail to assert or assert false any number of times, but should never assert true.
- `RGCondition.ONCE_TRUE_ALWAYS_TRUE`: This validator should assert true once, and then must always assert true after that. It can fail to assert or assert false before that.

Essentially, a validator can choose to do one of three things:
* Do nothing - this may be the case where you are waiting for an entity to appear. It is not false or true, but is internally marked as NOT_SET.
* Assert true - this is the case where the condition being checked is now true. Note that this doesn't mean pass necessarily, because a NEVER_TRUE condition that asserts as true is technically a FAIL
* Assert false - this is the case where the condition being checked is now false. This is a FAIL.

## Getting Started on your Own

Before getting feedback, I'd love to have you all actually try this to really see how it
feels. You can get started in literally minutes, all you need to do is:

```bash
# In RGBossRoom
git checkout prototype-rg-validators

# In RGUnityBots
git checkout prototype-rg-validators
```

Then open either `Assets/Scripts/RegressionGames/ValidateUI.cs` or `Assets/Scripts/RegressionGames/ValidateCharacters.cs` and start writing your own validators.
If you want to make your own new test file, you need to add it as a component to the RGOverlay object. **This is obviously not the final
approach - it seemed like overkill for the first pass, but these would be added a "validator" field in the sequence perhaps.**

Then, just open up the pane with the menu Regression Games > Validation Results.

## Full Examples

```csharp
using RegressionGames.Validation;
using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom.RegressionGames
{
    public class ValidateCharacters: RGValidateBehaviour
    {

        [RGValidate(RGCondition.NEVER_TRUE, 100)]
        void CheatsPopupPanelShouldNotExist()
        {
            var cheatsPopupPanel = GameObject.Find("CheatsPopupPanel");
            if (cheatsPopupPanel)
            {
                AssertAsTrue("CheatsPopupPanel was found");
            }
        }

        [RGValidate(RGCondition.EVENTUALLY_TRUE, 100)]
        void SomePotsShouldBeNearPlayer()
        {
            
            // The collective distance of all the pots from the player should be less than a certain amount
            var player = GameObject.FindWithTag("Player");
            var pots = FindObjectsOfType<Breakable>();

            var countNearby = 0;
            if (player != null && pots.Length > 0)
            {
                // Make sure there are at least 3 pots within 10 units of the player
                foreach (var pot in pots)
                {
                    if (Vector3.Distance(player.transform.position, pot.transform.position) < 10)
                    {
                        countNearby++;
                    }
                }
            }
            
            if (countNearby < 3)
            {
                AssertAsFalse("Less than 3 pots are near the player");
            }
            else
            {
                AssertAsTrue("At least 3 pots are near the player");
            }

        }
        
    }
}
```

```csharp
using System;
using RegressionGames.Validation;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Multiplayer.Samples.BossRoom.RegressionGames
{
    public class ValidateUI: RGValidateBehaviour
    {
        private bool profileDialogAppeared = false;
        private float previousParticleTime = float.NegativeInfinity;
        
        [RGValidate(RGCondition.EVENTUALLY_TRUE, 100)]
        void ProfileButtonAppears()
        {
            var profileButton = GameObject.Find("Profile Button");
            if (profileButton)
            {
                AssertAsTrue("Profile button has appeared");
            }
        }

        [RGValidate(RGCondition.EVENTUALLY_TRUE, 100)]
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
        
        [RGValidate(RGCondition.EVENTUALLY_TRUE, 100)]
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
        
        [RGValidate(RGCondition.ONCE_TRUE_ALWAYS_TRUE, 10)]
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

## My initial observations

* I feel that the attribute approach gives a really simple and clean way to write these validators. I know there
  was push back on this idea because of "just write a monobehaviour" aspect, but I do see this as being simpler
  as they would be run automatically and part of a sequence. If I was to write a MonoBehaviour instead directly,
  I'd need to manage results and activate it myself.
* I am not convinced by the types of conditions I have here. For instance, `ONCE_TRUE_ALWAYS_TRUE` was one I added
  later but felt weird.
* The "AssertAsFalse" and "AssertAsTrue" language is strange, especially in the context of NEVER_TRUE. I wonder if there
  is better language I can use.