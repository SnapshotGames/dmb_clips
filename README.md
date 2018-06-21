# Dumb Sequencing and Playback  

A dumb alternative to Unity3D's own 'smart' Animation, Timeline and Cinemachine systems.  

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
**load \<clip\>** - loads clip.  
**save \[optional_clip_name\]** - saves clip.  
**new** - creates a new empty clip.  
**import \<clip\>** - appends another clip to current clip.  
**replace_actor \<actor\> \[optional_actor\]** - if optional actor name is supplied replaces \<actor\> with [optional_actor], otherwise, replaces all actors with \<actor\>.  
**revert** - load last saved version of current clip.  
**shift \<seconds\>** - shifts clip samples with \<seconds\> seconds.  
**trim** - cut any empty time at the clip bounds.  

![screenshot](https://i.imgur.com/sHUU5de.png)
----
![vid](https://i.imgur.com/Ft3AgNi.gif)
----
Video of the tool: https://youtu.be/jr76VzZQUME

Dumb Clips Goals
----
Easy creation of tiny cutscenes (Clips) with multiple animated actors, camera splines and actor movement over paths.  
* Multiple tracks for samples.  
* Easy animation sampling and crossfades.  
* Easy path following using steering while playing animations in other tracks.  
* Easy camera 'rails' and cuts using splines and keys.    

The Sequencer (a.k.a the tool) works both in Play and Edit modes.  
Edit samples and paths while playing a Clip.  
Minimal Sequencer interface, directly inside the Scene Window.  
Clips are just a collection of Samples, each Sample class implementing specific action over time.  
Use only human readable data structures, leading to readable prefabs.  
No Custom Property Drawers and Editors.  
Simple Sequencer setup.  
Simple Playback embedding in games.  
Extend the Sequencer and playback by extending only the core (implementing specific Samples and playback routines), not the interface/inspector.
