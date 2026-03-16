We are making a game using Godot engine.

The game takes place on a elevated monorail track in the desert. A maglev train is driving along the track. Because the train is the focal point of the game, it doesnt move relative to the scene origin. Instead, the ground texture (and track texture) moves backwards to give the illusion of movement. Also, the support pillars for the track are always moving backwards, being destroyed behind and respawned in front. The trains "speed" does change over the course of the game, so these texture and pillar movements needs to be variable and controllable.

The player is controlling a turret which is attached to the back of a flying car. The car is flying alongside the train. The players view is first person. The player can control the speed of the car relative to the train, so it can move forwards and backwards over time. The max speed forward is very slow, while the max speed backwards is considerably faster.

The train has multiple containers attached to each carriage (except the Locomotive and Caboose). Each container has a number of clamps on its surface. The player can shoot at and destroy clamps. When all clamps are destroyed, the container will come loose and fall off the train. Each container has one cargo type, selected at random at game start, but the player cannot see the type to begin with. Containers also have hit points, and if destroyed they explode in a cloud of scrap metal, destroying them and their clamps.

Each time a container falls off the train, the trains speed increases. This actually means that the players maximum forward relative speed is reduced. After several containers have been removed, the player can no longer move forward (max relative forward speed == 0). After several move have been removed, the players max relative speed goes into the negative.

If the player falls too far behind the train (out of range of the turret gun) then the level ends and we move to an after action scene where the player sees what they collected and can spend it on resources.

The players turret has a secondary fire that shoots a beacon. If it hits a container, the container color changes to match its cargo type.

The players turret should have the following stats that are configurable in a config file. Damage, Rate of Fire, blast radius, bullet speed, ammo per clip, reload time. The turret can be reloaded an unlimited amount of times.

Also in the config file: min/max number of clamps per container. min/max per carriage, min/max number of carriages, min/max relative player velocity, car speed increase change rate (acceleration), car speed decrease change rate (deceleration), clamp Hitpoints, container hitpoints, beacon reload speed.

The after action screen for now lists how many of each cargo was detached from the train (not destroyed) and has a button to play with a new randomized train.