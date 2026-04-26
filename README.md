
 
# No Job Authors

##Item's final Quality is determined by whoever finishes the job!

If a pawn is machining or fabricating and gives up halfway through to go perform some other task, you are left with an Unfinished Thing that is locked to that author. This mod removes that lock, allowing any pawn to finish the job that was started by another.

Out of the box the mod behaves exactly as it always has. No settings need to be changed. Any pawn can pick up any unfinished item and finish it.

It should be compatible with any mod that generates unfinished things (eg. Core Drill, etc).

https://ludeon.com/forums/index.php?topic=47836.0


## Beta Features

The mod now includes several opt-in beta features that can be toggled in the mod settings menu. All of these are off by default and have no effect unless you enable them.

**Force Finish Before Starting New**
When this is on, pawns that are eligible to craft will first look for any existing unfinished items they can complete before starting a brand new crafting job. This helps prevent a pile-up of half-finished work on your benches.

**Only Apply to Non-Quality Items**
When this is on, the shared authoring behavior only applies to recipes that do not produce quality-rated items. Art, apparel, weapons, and anything else with a quality outcome will remain locked to its original crafter. Use this if you want shared authoring for industrial goods but not for your carefully trained artisans.

**Block Unfinished Items in Storage**
When this is on, storage buildings reject unfinished items going forward. This does not automatically uncheck any existing storage filter entries. Use the Clean current storage filters button in the settings to update existing storage filters so they disallow unfinished items.

**Finish It! Compatibility**
Requires the Finish It! mod by Xandrmoro. When this is on, the gizmo that Finish It provides for unfinished items will work correctly with shared authoring, finding an eligible pawn and sending them to complete the job.

**Achtung! Compatibility**
Requires the Achtung! mod. When this is on, pawns that have been force-assigned to other work by Achtung will not be pulled away from their forced tasks to go finish unfinished items.


## Mods That Need More Testing

The following mods have partial or diagnostic-only compatibility right now. If you use any of these with No Job Authors enabled, you can help by enabling the relevant toggle, reproducing whatever issue you experience, and then sharing your game log. Look for lines that start with [NoJobAuthors] in the log output.

**Life Lessons**
The Life Lessons mod hooks into crafting to grant proficiency experience. The interaction between its experience tracking and shared authoring has not been fully validated. If you use Life Lessons and notice pawns gaining unexpected experience (or not gaining it when they should) after finishing someone else's work, please report it with your log.

**Vanilla Psycasts Expanded**
VPE includes a Craft Timeskip ability that lets a psycaster instantly finish an unfinished item. Its fallback behavior when the original author is absent looks safe, but edge cases around the shared authoring patch have not been fully confirmed. If you use VPE and experience odd behavior when Craft Timeskip is used on an item whose author label reads Everyone, please report it.


## How to Share a Log

1. Enable the relevant beta toggle in mod settings
2. Reproduce the situation in game
3. Open the game menu and choose Upload Log File or navigate to your RimWorld log manually
4. Post the log along with a description of what happened in the forum thread, workshop comments, github issues section, or the discord https://discord.gg/CYJjE9nxA9 

OG Author Forum thread: https://ludeon.com/forums/index.php?topic=47836.0


