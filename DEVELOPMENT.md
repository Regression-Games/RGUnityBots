# Dev Tooling

## Getting Started

### Prerequisites

* Unity Hub - You'll want to install whatever version of Unity is necessary to open the src/RGUnityBots project.
* JetBrains Rider - Not required, but highly recommended for development.

### Setup

The first time you clone this repo you must run `script/bootstrap` to prepare your environment.
This installs our precommit hook so that you don't commit code that fails our formatting checks.
You can, and should, re-run this at any time to ensure your environment is up to date.

## Repo Layout

This repository contains the following folders:

* `src/gg.regression.unity.bots` - The RegressionGames Unity SDK package.
* `src/RGUnityBots` - A barebones 3D Unity game that is used to build and test the Unity SDK package.
* `samples/` - Additional sample games that use the RegressionGames Unity SDK package.
* `script/` - Scripts used to build and test the SDK. Based off the [Scripts To Rule Them All](https://github.com/github/scripts-to-rule-them-all) pattern.

## Building and testing the SDK

To build and test the SDK, open the `src/RGUnityBots` Unity project.
Building that project should build and validate all the code in the SDK.
Any tests for the SDK should be added to that project, and can be run from the Unity Test Runner.

## Useful Scripts:

### `script/bootstrap`

Validates that you have the required Unity version installed and installs any other dependencies.

```
Usage: scripts/bootstrap
Configures the repo for building and testing.
```

### `script/build`

Builds the `src/RGUnityBots` package, to check for compilation errors in our package.

```
Usage: scripts/build [-u|--unity-path <path>] [-b|--build-type <type>]
Builds the Unity project.

Options:
  -u, --unity-path <path>    Path to the Unity installation to use. Defaults to an autodetected path based on the version of RGUnityBots.
  -b, --build-type <type>    Type of build to perform. Defaults to the current platform.
     -b "Linux"              Builds a Linux standalone player.
     -b "macOS"              Builds a macOS standalone player.
     -b "Windows"            Builds a Windows standalone player.
```

### `script/test`

```
Usage: scripts/build [--unity-path <path>] [--skip-edit-mode] [--skip-play-mode] [--category <category>] [--out <output_path>]
Builds the Unity project.

Options:
  -u, --unity-path <path>    Path to the Unity installation to use. Defaults to an autodetected path based on the version of RGUnityBots.
  --skip-edit-mode           Skip running the Edit mode tests.
  --skip-play-mode           Skip running the Play mode tests.
  --category <category>      Run only tests in the given category. Defaults to all categories.
  --out <output_path>        Path to write the test results to. Defaults to "artifacts/test-results.xml".
```

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
