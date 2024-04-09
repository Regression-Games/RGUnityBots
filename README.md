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

Regression Games Unity Bots is an SDK that makes it easy to integrate powerful and useful bots into your game. 
Build bots to QA test your game, act as NPCs, compete against players in multiplayer, and determine 
optimal game balance. Our integration patterns, ready-to-go bots, and samples make it easy to get
started. Regression Games Unity Bots bots have been used to:

- Control enemy NPCs within a dungeon crawler
- Explore and exploit environments to find errors and issues within a platformer
- Play through puzzle games like Match-3 to determine difficulty
- QA test a MOBA game by testing ability interactions and cooldowns
- Determine optimal settings for speed and friction in a racing game by comparing bot behavior

## Quick Start - Add the package

In the Unity Editor, inside of **Window** > **Package Manager** > **+** > 
**Add package from git URL**, enter this URL (and restart your IDE after adding):

```
https://github.com/Regression-Games/RGUnityBots.git?path=src/gg.regression.unity.bots#v0.0.18
```

Once you add the package, create an account at https://play.regression.gg, then visit our [documentation site](https://docs.regression.gg) to get started!

## What is included in this package

- Useful abstractions and classes for making bots within Unity
- Easy integration approaches with [`RGState`](https://docs.regression.gg/studios/unity/unity-sdk/RGState) and [`RGAction`](https://docs.regression.gg/studios/unity/unity-sdk/RGAction) that allow bots to understand your game
- An [`RGOverlayCanvas`](https://docs.regression.gg/studios/unity/tutorials/first_tutorial#add-the-rgoverlaycanvas) for in-game control of starting and stopping bots
- An in-editor [timeline and replay feature](https://docs.regression.gg/studios/unity/unity-sdk/in-editor-replay) to see what your bots did during a run
- Examples for [running bots in CI/CD environments](https://docs.regression.gg/studios/unity/tutorials/github_actions)
- (Coming soon) Ready-to-go sample scenes for playing with existing bots

## FAQ

#### What languages do you support?

We currently support C# for bots running locally within your Unity runtime, but
also offer JavaScript and Typescript through our remote bot system. To make a 
bot based on JavaScript and Typescript, visit the 
[bot manager page](https://play.regression.gg/bots) to create a bot from a template.

#### How can I see what my bot did?

_Note: This is only applicable to our remote-running bot system, coming soon for local bots._

Every run with bots produces a replay file that can be downloaded from the 
[Bot History page](https://play.regression.gg/running-bots) on the Regression Games site.
Once you download the replay, you can view the **Bot Replay** interface from the
**Regression Games** menu in Unity, and load the replay to see what your bot did and saw.

#### Does the package cost money?

This package is free to use, but this is also an early preview of a much
grander vision for our bot/agent platform. Access to more advanced tools around
bot management, CI/CD, advanced templates, and other features will likely be paid
for in the future.

#### Does the package use machine learning?

This package does not currently use machine learning, but ML can be used 
within bots! You have full control over how the bots work, and we will be providing
ML tools and model architectures in the future.
