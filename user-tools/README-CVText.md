## Bot Segment Type

###  CVText
Disclaimer: The `CVText` type is still in an experimental phase and may provide inconsistent or unexpected results in some situations.  We are continuing to evaluate and tune this type.  See the [Limitations and Notes](#limitations-and-notes) section below for more information.
  - The `CVText` type can be used in Bot Segments for both `endCriteria` and/or `botAction`.  This type looks for the presence of the supplied text in the current frame of the game.  This type communicates with our remote AIService instance to perform the evaluation.  When a result is found, the AIService provides the bounding rect information to our SDK to confirm the existence and location of the supplied text in the current frame.

**TODO:** [Loom Video - Feel free to ignore this as I will be making these from my own demos]

## Configuration

### Key JSON fields

#### `endCriteria`
- `type` - `CVText` - tells the SDK to evaluate this type of criteria using the supplied data
- `transient` - `transient`=`true` means that this text can match at any time during the evaluation of this bot segment and that passing result will persist even if it takes multiple more frames before other criteria in this segment are matched.  `transient`=`false` means that his criteria and other non-transient criteria must all be true at the same time (any transient criteria must also have matched already).  For `CVText` evaluation, it is not recommended to mark `transient`=`false`.  **Transient should almost always be `true` for `CVText`**.
- `data` - The data json object that defines how to evaluate this `CVText` criteria.
  - `text` - The text to find.  Note that this algorithm will treat this as a set of words to find, NOT a sentence.  Thus if you have 'Create New Profile' on a single line on one button, but have 'Create' 'New' 'Profile' each on a separate line in different screen area, the algorithm has to choose which one to select.  The current method is to select the smallest bounding area containing all required words that is also `withinRect` (if specified).
  - `textMatchingRule` - One of `Matches` or `Contains`
    - `Matches` - Each word in the provided `text` must be found as a whole word in the results.  This is a very exact matching rule and can sometimes lead to inconsistent results in frames with poor in game lighting on the text or low text contrast.
    - `Contains` - Each word in the provided `text` must be found as a part of a word in the results.  This is a looser matching rule and if often used instead of `Matches` for more stable results.  For example "Time" would match to "Timer", "Time", "Times", "Timed", "Time", etc in the frame.  This may not seem ideal, but in most game situations, the contrast or text layout isn't always clearly identifiable and extra or incorrect letters may consistently be found for the text you are looking for in a specific frame of the game.  When you encounter those situations, you can adjust your game to give better text clarity/contrast to your users and/or you can utilize `Contains` instead of `Matches`.
  - `textCaseRule` - One of `Matches` or `Ignore`
    - `Matches` - (NOT CURRENTLY SUPPORTED - See the [Limitations and Notes](#limitations-and-notes) section below for details.) The result must match the case of the specified text exactly.
    - `Ignore` - The specified text is matched without considering capitalization.  This option should be used always.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly tranform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied text data.  The values are defined in pixels.

#### `botAction`
- `type` - `Mouse_CVText` - tells the SDK to evaluate this type of criteria using the supplied data and perform the specified mouse actions at the center of the found `rect`
- `data` - The data json object that defines how to evaluate this `CVText` criteria.
  - `text` - The text to find.  Note that this algorithm will treat this as a set of words to find, NOT a sentence.  Thus if you have 'Create New Profile' on a single line on one button, but have 'Create' 'New' 'Profile' each on a separate line in different screen area, the algorithm has to choose which one to select.  The current method is to select the smallest bounding area containing all required words that is also `withinRect` (if specified).
  - `textMatchingRule` - One of `Matches` or `Contains`
    - `Matches` - Each word in the provided `text` must be found as a whole word in the results.  This is a very exact matching rule and can sometimes lead to inconsistent results in frames with poor in game lighting on the text or low text contrast.
    - `Contains` - Each word in the provided `text` must be found as a part of a word in the results.  This is a looser matching rule and if often used instead of `Matches` for more stable results.  For example "Time" would match to "Timer", "Time", "Times", "Timed", "Time", etc in the frame.  This may not seem ideal, but in most game situations, the contrast or text layout isn't always clearly identifiable and extra or incorrect letters may consistently be found for the text you are looking for in a specific frame of the game.  When you encounter those situations, you can adjust your game to give better text clarity/contrast to your users and/or you can utilize `Contains` instead of `Matches`.
  - `textCaseRule` - One of `Matches` or `Ignore`
    - `Matches` - (NOT CURRENTLY SUPPORTED - See the [Limitations and Notes](#limitations-and-notes) section below for details.) The result must match the case of the specified text exactly.
    - `Ignore` - The specified text is matched without considering capitalization.  This option should be used always.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly tranform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied text data.  The values are defined in pixels.
  - `actions` - The list of mouse actions to take at the center point of the returned `rect`.

## Example Segment JSON

### `endCriteria`

```json
{
    "name":"CV Text Criteria: Create New Profile Button Text",
    "sessionId":"12345",
    "endCriteria":[
        {
            "type":"CVText",
            "transient":true,
            "data":{
                "text":"CREATE NEW PROFILE",
                "textMatchingRule":"Contains",
                "textCaseRule":"Ignore"
                "withinRect": {
                    "screenSize":{"x": 1656, "y": 724},
                    "rect":{"x": 900, "y": 110, "width": 350, "height": 50}
                } 
            }
        }
    ],
    "botAction":{}
}
```

### `botAction`

```json
{
    "name": "CV Text Action: Click Create New Profile Button",
    "description":"Moves the mouse over the Create New Profile button text, then clicks and releases on the button. Criteria waits for the action to complete.",
    "sessionId":"67890",
    "endCriteria": [
        {"type":"ActionComplete" }
    ],
    "botAction":{    
        "type":"Mouse_CVText",
        "data": {
            "type":"CVText",
            "transient":true,
            "data":{
                "text":"CREATE NEW PROFILE",
                "textMatchingRule":"Contains",
                "textCaseRule":"Ignore"
                "withinRect": {
                    "screenSize":{"x": 1656, "y": 724},
                    "rect":{"x": 900, "y": 110, "width": 350, "height": 50}
                } 
            }
            "actions": [
                {"leftButton":false,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0},"duration":2.0 },
                {"leftButton":true,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0} },
                {"leftButton":false,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0} }
            ]
        }
    }
}
```

## Demos
**TODO:** Link to a public git repo files that are using this feature. **Feel free to skip this as I might be making a lot of these myself.**
- `endCriteria` - (Non-Public) https://github.com/Regression-Games/RGBossRoom/tree/main/Assets/RegressionGames/Resources/BotSegments/computer_vision/image_match_criteria_menu_changeprofilebutton
- `botAction` - (Non-Public) https://github.com/Regression-Games/RGBossRoom/tree/main/Assets/RegressionGames/Resources/BotSegments/computer_vision/image_match_action_changeprofilebutton

## Limitations and Notes
The `CVText` type is still in an experimental phase and may provide inconsistent or unexpected results in some situations.
- `textCaseRule` must always be set to `Ignore`.  The AIService does not currently consider capitalization in its results.
- Text with low contrast relative to its background may not be detected or may detect incorrect characters
- Very small or very large text may not be detected or may detect incorrect characters
- Less common fonts may not be detected or may detect incorrect characters
