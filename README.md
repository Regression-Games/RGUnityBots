# Regression Games Unity Bots
[![Static Badge](https://img.shields.io/badge/Latest%20Version-0.0.22-rc1-blue)](https://docs.regression.gg)
[![Join our Discord](https://img.shields.io/badge/Join%20our%20Discord-8A2BE2)](https://discord.com/invite/925SYVse2H)
[![Changelog](https://img.shields.io/badge/Visit%20the%20Changelog-orange)](https://docs.regression.gg/changelog)

<img 
  alt="A demo of bots and our replay feature"
  width="500px"
  style="text-align: center; margin: auto auto"
  src="imgs/bossroom_example.gif"
/>

## What is Regression Games Unity Bots?

Regression Games Unity Bots is an SDK that makes it easy to implement automated tests for your game.
It provides a variety of tools for regression testing, from robust input playback to LLM-powered bot-creation interfaces.

Some of the main features include:

- Instant data extraction - begin analyzing game state immediately, no coding required!
- A variety of bot strategies, from simple rule-based MonoBehaviours to complex behavior trees
- A Smart Playback tool that records gameplay and applies automated validations on replay
- An in-game bot management overlay for easy control over bots and recording features

_View the full documentation at [docs.regression.gg](https://docs.regression.gg)_

## Quick Start - Add the package

In the Unity Editor, open the Package Manager by navigating to  **Window** > **Package Manager**. 
Then add a new package with  **+** > **Add package from git URL** and paste the following URL:

```
https://github.com/Regression-Games/RGUnityBots.git?path=src/gg.regression.unity.bots#v0.0.22-rc1
```

Once the package has been added, restart the Unity Editor and create an account at 
https://play.regression.gg. Visit our [documentation site](https://docs.regression.gg) to get started!

## FAQ

### Does this work with CI/CD pipelines?

It does! Visit our [tutorial on using GitHub Actions and GameCI](https://docs.regression.gg/tutorials/github-actions) for more info.

#### What languages do you support?

All of our tools are based in C#. Bots handwritten using our API are defined as C# Monobehaviours, and our various tools (code, low-code, and no-code) either present C# to the user or execute C# under-the-hood.

#### Does this package cost money?

This package is free to use, but this is also an early preview of a much
grander vision for our bot/agent platform. Access to more advanced tools around
bot management, CI/CD, advanced templates, and other features will likely be paid
for in the future.
