# Dumb Sequencing and Playback
A dumb alternative to Unity3D's own 'smart' Animation, Timeline and Cinemachine systems.
----
The core is the DMBSequencer tool which supports animation playback with crossfades, path following, easy camera cuts, camera position and lookat lerp along splines. The resulting clips are simple unity prefabs with no references in them, only strings and numbers.

Installation
----
Just copy/git clone dmb_clips inside your Assets folder.

Running the Example
----
1) Open the scene Assets/dmb_clips/Example/Sequencer.unity. 
2) Click on the sequencer timeline in the Scene View. 
3) In the Command Line field type 'load \<partial clip name\>' and press Enter. For example 'load big guy' will load the big_guy_github.clip.prefab for editing.

Some useful commands
----
new -- creates a new empty clip.  
import \<clip\> -- appends another clip to current clip.  
load \<clip\> -- loads clip.  
save \[optional_clip_name\] -- saves clip.  
replace_actor \<actor\> [optional_actor] -- if optional actor name is supplied replaces \<actor\> with [optional_actor], otherwise, replaces all actors with \<actor\>.  
revert -- load last saved version of current clip.  
shift \<seconds\> -- shifts clip samples with \<seconds\> seconds.  
trim -- cut any empty time at the clip bounds.  

![screenshot](https://i.imgur.com/sHUU5de.png)
----
![vid](https://i.imgur.com/Ft3AgNi.gif)
----
Video of the tool: https://youtu.be/jr76VzZQUME
