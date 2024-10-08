## Bot Segment Type

###  CVImage

Disclaimer: The `CVImage` type is still in an experimental phase and may provide inconsistent or unexpected results in many situations.  We are continuing to evaluate and tune this type.  See the [Limitations and Notes](#limitations-and-notes) section below for more information.
  - The `CVImage` type can be used in Bot Segments for both `endCriteria` and/or `botAction`.  This type looks for the presence of the supplied image in the current frame of the game until a frame contains the supplied image.  This type communicates with our remote server instance to perform the evaluation.  When a result is found, the server provides the bounding rect information to our SDK to confirm the existence and location of the supplied image in the current frame.

**TODO:** [Loom Video - Feel free to ignore this as I will be making these from my own demos]

## Configuration

### Key JSON fields

#### `endCriteria`
- `type` - `CVImage` - tells the SDK to evaluate this type of criteria using the supplied data
- `transient` - `transient`=`true` means that this image can match at any time during the evaluation of this bot segment and that passing result will persist even if it takes multiple more frames before other criteria in this segment are matched.  `transient`=`false` means that this criteria and other non-transient criteria must all be true at the same time (any transient criteria must also have matched already).  **Transient should almost always be `true` for `CVImage`**.  `CVImage` evaluation is a largely asynchronous process that can take multiple frames to complete, thus setting `transient`=`false` for the criteria to match within a single frame can lead to situations where the criteria matched on the frame when the request was made, but are no longer matching by the time the result comes back.  Setting `transient`=`true` allows the needed flexibility for this asynchronous operation to pass more easily, especially when used  combination with other `endCriteria`.
- `data` - The data json object that defines how to evaluate this `CVImage` criteria.
  - `imageData` - The image data in one of the following formats...
    - The base64 encoded string of the JPG image data - This must be the entire JPG image file, not just the visible bytes.
    - A file:// path to a JPG or PNG image - Be careful when using relative paths as these will be interpreted differently in the Editor vs Runtime builds.
    - A resource:// path to a READABLE Texture2D in one of your project's Resources folders.
    - A resource:// path to a .bytes TextAsset in one of your project's Resources folders that is a JPG or PNG saved with a .bytes file extension.  This can be used if you do not want Unity to import your image as a Texture.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly transform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied image data.  The values are defined in pixels.

#### `botAction`
- `type` - `Mouse_CVImage` - tells the SDK to evaluate this type of criteria using the supplied data and perform the specified mouse actions at the center of the found `rect`
- `data` - The data json object that defines how to evaluate this `CVImage` criteria.
  - `imageData` - The image data in one of the following formats...
    - The base64 encoded string of the JPG image data - This must be the entire JPG image file, not just the visible bytes.
    - A file:// path to a JPG or PNG image - Be careful when using relative paths as these will be interpreted differently in the Editor vs Runtime builds.
    - A resource:// path to a READABLE Texture2D in one of your project's Resources folders.
    - A resource:// path to a .bytes TextAsset in one of your project's Resources folders that is a JPG or PNG file saved with a .bytes file extension.  This can be used if you do not want Unity to import your image as a Texture.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly tranform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied image data.  The values are defined in pixels.
  - `actions` - The list of mouse actions to take at the center point of the returned `rect`.

### How to create a CVImage BotSegment

1. Use a screenshot tool to capture the target area of your game and save this as a JPG file (PNG/BMP/etc are NOT supported at this time).
   - For this example we saved the file as `change_profile_button.jpg`
   - ![Change Profile Button](./sample_images/change_profile_button.jpg)

```shell
CURRENT_PATH> python encode_jpg_base64.py sample_images/tree.jpg
/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=
```
or
```shell
CURRENT_PATH> ./encode_jpg_base64.sh sample_images/tree.jpg
/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=
```

3. Create a new bot segment json file and copy the base64 encoded output as the `imageData` of your CVImage criteria. The `withinRect` field is optional and can be used to limit the search to specific regions of the screen.  Be aware that the `rect` field is defined in coordinates where the `x` and `y` coordinates are 0 at the bottom left of the screen.  The `screenSize` field defines the overall coordinate space for the specified `rect` and is used to linearly transform the `rect` into the current game resolution during runtime analysis thus suporting varying resolutions.

## Example Segment JSON

### `endCriteria`

- Using base64 byte[]
```json
{
    "name":"CV Image Criteria: Menu Change Profile Button",
    "endCriteria":[
        {
            "type":"CVImage",
            "transient":true,
            "data":{
                "imageData":"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",
                "withinRect": {
                    "screenSize":{"x":1653,"y":714},
                    "rect":{"x":1250,"y":210,"width":250,"height":275}
                } 
            }
        }
    ],
    "botAction":{}
}
```

- Using file:// path (??? represents the path to this folder on your system)
```json
{
    "name":"CV Image Criteria: Menu Change Profile Button",
    "endCriteria":[
        {
            "type":"CVImage",
            "transient":true,
            "data":{
                "imageData":"file://???/sample_images/change_profile_button.jpg",
                "withinRect": {
                    "screenSize":{"x":1653,"y":714},
                    "rect":{"x":1250,"y":210,"width":250,"height":275}
                } 
            }
        }
    ],
    "botAction":{}
}
```

### `botAction`

- Using base64 byte[]
```json
{
    "name": "CV Image Action: Click Menu Change Profile Button",
    "description":"Moves the mouse over the change profile button, then clicks and releases on the button. Criteria waits for the action to complete.",
    "endCriteria": [
        {"type":"ActionComplete", "transient":false, "data":{}}
    ],
    "botAction":{    
        "type":"Mouse_CVImage",
        "data": {
            "imageData":"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",
            "withinRect": null,
            "actions": [
                {"leftButton":false,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0},"duration":2.0 },
                {"leftButton":true,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0} },
                {"leftButton":false,"middleButton":false,"rightButton":false,"forwardButton":false,"backButton":false,"scroll":{"x":0.0,"y":0.0} }
            ]
        }
    }
}
```

- Using resoure:// path (note that the `sample_images` folder must be under a Resources folder in your project)
```json
{
    "name": "CV Image Action: Click Menu Change Profile Button",
    "description":"Moves the mouse over the change profile button, then clicks and releases on the button. Criteria waits for the action to complete.",
    "endCriteria": [
        {"type":"ActionComplete" }
    ],
    "botAction":{    
        "type":"Mouse_CVImage",
        "data": {
            "imageData":"resource://sample_images/change_profile_button",
            "withinRect": null,
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
The `CVImage` type is still in an experimental phase and may provide inconsistent or unexpected results in many situations.
- Multiple matches of the specified image within a frame may provide incorrect or inconsistent result bounds.
- False positives or incorrect result bounds may occur if pixel regions similar to the specified image exist within a frame.