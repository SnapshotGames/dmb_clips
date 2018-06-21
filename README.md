# Dumb Sequencing and Playback
A dumb alternative to Unity3D's own 'smart' Animation, Timeline and Cinemachine systems.
----
The core is the DMBSequencer tool which supports animation playback with crossfades, path following, easy camera cuts, camera position and lookat lerp along splines. The resulting clips are simple unity prefabs with no references in them, only strings and numbers.

Installation
----
Just copy/git clone dmb_clips inside your Assets folder.

Running the Example
----
Click on the sequencer timeline in the Scene View. In the Command Line field type 'load \<partial clip name\>'. For example 'load big guy' will load the big_guy_github.clip.prefab for editing.

![screenshot](https://i.imgur.com/sHUU5de.png)
----
![vid](https://i.imgur.com/Ft3AgNi.gif)
----
Video of the tool: https://youtu.be/jr76VzZQUME
