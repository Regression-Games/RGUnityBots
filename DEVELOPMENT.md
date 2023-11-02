# Dev Tooling

## Saving a new sample from a development environment

In order to make changes to the sample scenes within the SDK, you need to create a new Unity project and
import the samples into your project. We provide a script that allows you to save your completed changes
back into the SDK.

Right now, we only support one sample (ThirdPersonDemoURP), but over time more will be added - right now,
this code only supports this sample.

Run the following command and follow the prompts to save your changes.

```bash
cd dev-tools
./save_sample_into_sdk.sh
> Enter the full path to the sample in your imported sample project: <COPY THE ABSOLUTE PATH OF YOUR SAMPLE HERE>
Copying /Users/you/SampleDemo/Assets/ThirdPersonDemoURP into /Users/you/RGUnityBots/Samples~
```