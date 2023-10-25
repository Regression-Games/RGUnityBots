# RG Data Collection

_Note: This is not info on the implementation, but rather the goals (i.e. not all of this is implemented,
but this is where I'd like to get it to).

The RG Data Collection module includes code for automatically collecting information from both
Regression Games bots and the Unity scene. This data is collected into a universal format that
can then be understood later within Regression Games products. Some of the data that gets collected
includes:

* General scene information
* All state information from bots
* Screenshots and video data from the run
* Stack trace and error information
* Performance information related to CPU, memory, GPU, and network

## Overview

All data is collected as part of an `RGSession`. An RGSession is automatically started when a bot is started,
or can be manually started from a Unity Test Runner file. The session tracks all information, which is stored
in timestamped data structures. For instance, there is a history of screenshots, history of states, etc...