In this update we need to add sound effects and music to the game and a main menu with save system.

The main menu has an image background. Generate a placeholder image for the artist to replace.
The options in the menu are:

## saves
Three save slots. If unsued, they show a + symbol. If used they show the number of raids played on that save, and the date of the last save action.
Each slot has a thinner button under it that shows a trash can icon. It is greyed out if the save is unused. Clicking it shows a dialog asking if you are sure you want to delete the save.
Clicking a used save slot button will continue the save
Clicking an unused save slot will start a new game in that slot.

## options
clicking this opens a settings menu with options for changing the volume of music and sfx, muting the sound/music and has a section for rebinding the keys.
There are also save (save and go back) and cancel (dont save and go back)

## quit
Quits the game.

For the sound effects, i dont know how to implement sound in Godot. Please suggest a system. The main thing for me is that each sound is imported from the assets folder. I need you to make placeholder sound files with suitable names for the artist to replace. Each sound name needs to be configurable in the config, because maybe the artist will want to use the same sound in some places. Volume has to be controllable via the settings menu, but we also need global sound volume tweaking values in the config.

Music should be in the asset/music folder. The config should have a list of music names that it tries to import for main menu, raid screen, and after action screen. There should be proper fade out/transition that is not annoying between screens. 

It has to be easy to add new sounds to things. Write a skill for adding new sounds.

We need sound on the following interactions/events:
Event sounds:
train zoom off
player shoot
player bullet hit something damagable
player bullet hit something non-damagable
drone destroyed
turret destroyed
drone deployed
turret change to active state
turret change to inactive state
container detached
clamp destroyed
container destroyed
player shield hit, no damage
player car hit
incoming cliff warning

loop sounds:
Car is accelerating
Car is decellerating

UI sound: 
container click to open
container is opened
generic button click
positive click for options save
negative click for options back
buy/apply upgrade
resource arrives at resource counter