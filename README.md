# Dumb Sequencing and Playback for Unity3D

Tiny cutscenes creation tool, with animation crossfades, path following and camera splines.

Installation
----
Copy or git clone dmb_clips inside your Assets folder. 

Running the Example
----
1) Make sure the 'Clips Parent Directory' field in the inspector points to the proper Resources folder.
2) Open the scene Assets/dmb_clips/Example/Sequencer.unity. 
3) Click on the sequencer timeline in the Scene View. 
4) In the Command Line field type 'load \<partial clip name\>' and press Enter. For example 'load big guy' will load the big_guy_github.clip.prefab for editing.

Some useful commands
----
**load \<clip\>** - loads clip.  
**save \[optional_clip_name\]** - saves clip.  
**new** - creates a new empty clip.  
**import \<clip\>** - appends another clip to current clip.  
**replace_actor \<actor\> \[optional_actor\]** - if optional actor name is supplied replaces \<actor\> with [optional_actor], otherwise, replaces all actors with \<actor\>.  
**revert** - load last saved version of current clip.  
**shift \<seconds\>** - shifts clip samples with \<seconds\> seconds.  
**trim** - cut any empty time at the clip bounds.  

![screenshot](https://i.imgur.com/kPRGsC9.png)
----
![vid](https://i.imgur.com/Ft3AgNi.gif)
----
Video of the tool: https://youtu.be/jr76VzZQUME
