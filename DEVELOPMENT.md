# Dev Tooling

## Saving a new sample from a development environment

### To make changes to the sample scenes within the SDK.
1. Create a new Unity Project. Make sure the project's Render Pipeline matches the sample you are importing.
2. Import the Regression SDK
3. Import the sample into the project
4. Make changes to sample
5. Run the provided`.sh` script. This will copy the sample you've edited `Assets/{SampleName}` to the 
SDK `UnityBots/Samples~/{SampleName}`.
6. Commit and push changes

Right now, we only support one sample: `ThirdPersonDemoURP`, but over time more will be added.

Run the following command and follow the prompts to save your changes.

```bash
cd dev-tools
./save_sample_into_sdk.sh
> Enter the full path to the sample in your imported sample project: <COPY THE ABSOLUTE PATH OF YOUR SAMPLE HERE>
Copying /Users/you/SampleDemo/Assets/ThirdPersonDemoURP into /Users/you/RGUnityBots/Samples~
```