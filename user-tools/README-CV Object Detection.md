## Bot Segment Type

###  CVObjectDetection

Disclaimer: The `CVObjectDetection` type is still in an experimental phase and may provide inconsistent or unexpected results in many situations.  We are continuing to evaluate and tune this type.  See the [Limitations and Notes](#limitations-and-notes) section below for more information.
  - The `CVObjectDetection` type can be used in Bot Segments for both `endCriteria` and/or `botAction`.  This type looks for the presence of an object in the current frame of the game until a frame contains the object. The object can either be specified by an image or by text.  This type communicates with our remote tool instance to perform the evaluation.  When a result is found, the tool provides the bounding rect information to our SDK to confirm the existence and location of the supplied image in the current frame.

**TODO:** [Loom Video - Feel free to ignore this as I will be making these from my own demos]

#### **CVObjectDetection via textQuery**

When a `textQuery` is specified the tool will search for the specified object in the image. For example, if the textQuery is "Dog" then the tool will look for a dog in the image. If a dog is detected then the bounding box of the dog will be returned.

This approach is useful when you want to detect an object that can look differently in the game. For example, you might want to click on a "Tree" but there might be several types of trees in the game, and you do not want to differentiate between them. This is also useful when the orientation of the object in the camera view is dynamic and it can't directly match a static image. If you know exactly what the tree might look like (such as a static sprite) then [CV Image](user-tools\README-CVImage.md) might be a better option for you.


#### **CVObjectDetection via imageQuery**

When an `imageQuery` is specified the tool will search for the type of object that is contained in the image. For example, if the `imageQuery` is an image of a dog then the tool will first identify that the main object in the `imageQuery` is a dog, then it will look for a dog in the image. If a dog is detected then the tool will return the bounding box of the dog.

This approach is useful when you have an image of an asset, however the asset can look different in the game. For example, the asset might be rotated, have an effect on it, or the rendering details might be different.

## Configuration

### Key JSON fields

#### `endCriteria`
- `type` - `CVObjectDetection` - tells the SDK to evaluate this type of criteria using the supplied data
- `transient` - `transient`=`true` means that this image can match at any time during the evaluation of this bot segment and that passing result will persist even if it takes multiple more frames before other criteria in this segment are matched.  `transient`=`false` means that this criteria and other non-transient criteria must all be true at the same time (any transient criteria must also have matched already).  **Transient should almost always be `true` for `CVObjectDetection`**.  `CVObjectDetection` evaluation is a largely asynchronous process that can take multiple frames to complete, thus setting `transient`=`false` for the criteria to match within a single frame can lead to situations where the criteria matched on the frame when the request was made, but are no longer matching by the time the result comes back.  Setting `transient`=`true` allows the needed flexibility for this asynchronous operation to pass more easily, especially when used  combination with other `endCriteria`.
- `data` - The data json object that defines how to evaluate this `CVObjectDetection` criteria. **Either imageQuery or textQuery can be specified, you can not use both.**
  - `imageQuery` - The image describing the object to search for in the current frame.  The image data must be in one of the following formats...
    - The base64 encoded string of the JPG image data - This must be the entire JPG image file, not just the visible bytes.
    - A file:// path to a JPG or PNG image - Be careful when using relative paths as these will be interpreted differently in the Editor vs Runtime builds.
    - A resource:// path to a READABLE Texture2D in one of your project's Resources folders.
    - A resource:// path to a .bytes TextAsset in one of your project's Resources folders that is a JPG or PNG saved with a .bytes file extension.  This can be used if you do not want Unity to import your image as a Texture.
  - `textQuery` - The string describing the object to search for in the current frame.
  - `threshold` - An optional (can be null/undefined) field to accept a returned match from the object detection model. Returned matches with a confidence score less than this threshold are ignored. Currently, this is only supported for usage with `textQuery`.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly transform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied image data.  The values are defined in pixels.

#### `botAction`
- `type` - `Mouse_ObjectDetection` - tells the SDK to evaluate this type of criteria using the supplied data and perform the specified mouse actions at the center of the found `rect`
- `data` - The data json object that defines how to evaluate this `CVObjectDetection` criteria. Either imageQuery or textQuery can be specified, you can not use both.
  - `imageQuery` - The image describing the object to search for in the current frame.  The image data must be in one of the following formats...
    - The base64 encoded string of the JPG image data - This must be the entire JPG image file, not just the visible bytes.
    - A file:// path to a JPG or PNG image - Be careful when using relative paths as these will be interpreted differently in the Editor vs Runtime builds.
    - A resource:// path to a READABLE Texture2D in one of your project's Resources folders.
    - A resource:// path to a .bytes TextAsset in one of your project's Resources folders that is a JPG or PNG saved with a .bytes file extension.  This can be used if you do not want Unity to import your image as a Texture.
  - `textQuery` - The string describing the object to search for in the current frame.
  - `threshold` - An optional (can be null/undefined) field to accept a returned match from the object detection model. Returned matches with a confidence score less than this threshold are ignored. Currently, this is only supported for usage with `textQuery`.
  - `withinRect` - An optional (can be null/undefined) field to limit the search area to a specific pixel region of the current frame.  The SDK will linearly transform the supplied `rect` to fit the current resolution using the `screenSize` as the initial reference resolution.
    - `screenSize` - The reference resolution in pixels which defines the screen space that `rect` is defined within.
    - `rect` - The position (x=0, y=0 is bottom left) and size (width, height) of the rectangle that must contain the supplied image data.  The values are defined in pixels.
  - `actions` - The list of mouse actions to take at the center point of the returned `rect`.

### How to create a CVObjectDetection Image Query using base64 Encoding

1. Find the image you want to use as your query and save it as a JPG file (PNG/BMP/etc are NOT supported at this time).
   - For this example we saved the file as `tree.jpg`
   - ![Tree](./sample_images/tree.jpg)

2. Use the `encode_jpg_base64.py` or `encode_jpg_base64.sh` script in this directory to encode the JPG as a base64 string.  The output will be written to STDOUT.
```shell
 CURRENT_PATH> python encode_jpg_base64.py sample_images/tree.jpg
/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=
```
or
```shell
 CURRENT_PATH> ./encode_jpg_base64.sh sample_images/tree.jpg
/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=
```

3. Create a new bot segment json file and copy the base64 encoded output as the `imageQuery` of your CVObjectDetection criteria.


## Example Segment JSON

### `endCriteria`

```json
{
    "name":"CV Object Detection Criteria: Tree",
    "endCriteria":[
        {
            "type":"CVObjectDetection",
            "description":"Checks for the presence of a tree.",
            "transient":true,
            "data":{
                "imageQuery":"/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=",
                or
                "imageQuery":"file://???/sample_images/tree.jpg",
                or  
                "imageQuery":"resource://sample_images/tree.jpg",
                or
                "textQuery": "Tree",

                "threshold": 0.4, # Only valid for textQuery.
                "withinRect": {
                    "screenSize":{"x":1920,"y":1080},
                    "rect":{"x":1250,"y":210,"width":250,"height":275}
                } 
            }
        }
    ],
    "botAction":{}
}
```

- Using file:// path (??? represents the path to this folder on your system)
- Using resoure:// path (note that the `sample_images` folder must be under a Resources folder in your project)

### `botAction`

```json
{
    "name": "CV Object Detection Action: Click on a tree",
    "description":"Moves the mouse over the tree, then clicks and releases on the tree. Criteria waits for the action to complete.",
    "endCriteria": [
         {"type":"ActionComplete", "transient":false, "data":{}}
    ],
    "botAction":{    
        "type":"Mouse_ObjectDetection",
        "data": {
            "imageQuery":"/9j/4AAQ...<rest of the base64 encoded image data>...VLj3ieS/9k=",
            or
            "imageQuery":"file://???/sample_images/tree.jpg",
            or  
            "imageQuery":"resource://sample_images/tree",
            or
            "textQuery": "Tree",

            "threshold": 0.4, # Only valid for textQuery.
            "actions": [
                {"leftButton":false, "duration":0.1}, # Move the mouse to the center of the tree
                {"leftButton":true, "duration":0.1},   # Click the left mouse button
                {"leftButton":false, "duration":0.1}   # Release the left mouse button
            ]
        }
    }
}
```
- Using file:// path (??? represents the path to this folder on your system)
- Using resoure:// path (note that the `sample_images` folder must be under a Resources folder in your project)

## Demos
**TODO:** Link to a public git repo files that are using this feature. **Feel free to skip this as I might be making a lot of these myself.**
- `endCriteria` - (Non-Public) https://github.com/Regression-Games/RGBossRoom/tree/main/Assets/RegressionGames/Resources/BotSegments/computer_vision/image_match_criteria_menu_changeprofilebutton
- `botAction` - (Non-Public) https://github.com/Regression-Games/RGBossRoom/tree/main/Assets/RegressionGames/Resources/BotSegments/computer_vision/image_match_action_changeprofilebutton

## Limitations and Notes
The `CVObjectDetection` type is still in an experimental phase and may provide inconsistent or unexpected results in many situations.
- Multiple matches of the specified object within a frame will only return one at random.
- CVObjectDetection via ImageQuery has a very high false positive rate. We are continuing to evaluate and tune this type.
- CVObjectDetection via ImageQuery selects the most common object in the query image. If the query image contains multiple objects, (such as a cat and a dog) it will only select one--whichever is more prominent.