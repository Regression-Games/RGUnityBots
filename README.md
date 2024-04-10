# Regression Games Unity Bots
[![Static Badge](https://img.shields.io/badge/Latest%20Version-0.0.18-blue)](https://docs.regression.gg)
[![Join our Discord](https://img.shields.io/badge/Join%20our%20Discord-8A2BE2)](https://discord.com/invite/925SYVse2H)
[![Changelog](https://img.shields.io/badge/Visit%20the%20Changelog-orange)](https://docs.regression.gg/changelog)

<img 
  alt="A demo of bots and our replay feature"
  width="500px"
  style="text-align: center; margin: auto auto"
  src="imgs/bossroom_example.gif"
/>

## What is Regression Games Unity Bots?

Regression Games Unity Bots is an SDK that makes it easy to implement automated tests within your game.
It provides a variety of tools, from robust session recording playback to LLM-powered behavior tree bots, 
to help you quickly implement functional and regression tests within your game.

Some of the main features include:

- Instant data extraction - begin analyzing game state immediately after adding our SDK, no coding required!
- A variety of agent types are supported, from simple rule-based MonoBehaviours to LLM-powered behavior trees for low-code agent building
- A Smart Playback tool can be used to record a game session and replay it later as a test scenario
- Easily swap between AI implementations using our bot manager overlay

_View the full documentation at [docs.regression.gg](https://docs.regression.gg)_

## Quick Start - Add the package

In the Unity Editor, inside of **Window** > **Package Manager** > **+** > 
**Add package from git URL**, enter this URL (and restart your IDE after adding):

```
https://github.com/Regression-Games/RGUnityBots.git?path=src/gg.regression.unity.bots#v0.0.18
```

Once you add the package, create an account at https://play.regression.gg, then visit our [documentation site](https://docs.regression.gg) to get started!

## Does this work with CI/CD pipelines?

It does! Visit our [tutorial for using GitHub Actions and GameCI](https://docs.regression.gg/tutorials/github-actions) for more info.

## FAQ

#### What languages do you support?

If you'd like to write your own bots, we support C# MonoBehaviours as the basis of these agents.

#### Does the package cost money?

This package is free to use, but this is also an early preview of a much
grander vision for our bot/agent platform. Access to more advanced tools around
bot management, CI/CD, advanced templates, and other features will likely be paid
for in the future.
