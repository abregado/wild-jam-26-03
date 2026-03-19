When the game starts, i want to have an explanatory cutscene play out first.

When the scene loads, the players car is not yet spawned, or it is invisible (up to you which is easier). Either way the player has no control of the car.

During the cutscene, no obstacle sections will spawn.

The camera sweeps in from behind and on the left of the train to a viewpoint that focuses on the locomotive looking back along the train. The camera then sweeps down the train to each of these points and stops, displaying some text to the player:

Waypoint 1: 
What is focused: The first container on the left side of the train that doesn't contain Scrap. 
What text is shown: "Containers, they are full of loot. Use a Beacon to see what is inside."

Waypoint 2:
What is focused: The first container on the left side of the train that contains Scrap.
What text is shown: "Some containers are filled with useless Scrap. Ignore them."

Waypoint 3:
What is focused: The top most Clamp of the first container on the left side of the train
What text is shown: "Shoot clamps to detach a container"

Waypoint 4:
What is focused: The first Deployer.
What text is shown: "Deploys enemy drones to slow you down."

Waypoint 5:
What is focused:The first Turret
What text is shown: "Will defend adjacent carriages."

Waypoint 6:
What is focused: Caboose
What text is shown: "If you fall too far behind the train, the raid is over".

The waypoints come in whichever order they appear on the train, so Waypoint 4 might come before Waypoint 3.

Make sure that the first train spawned has one deployer and one turret.

Once the waypoints are done, Spawn the player car at the front of the train. The camera moves behind the train turns to face the Player car and then moves to put the car in Focus. The text "After each raid you can buy upgrades" is shown.

The text is shown in a box that is centered on the middle of the right half of the screen. The object that is being focused is positioned in the center of the left half of the screen.

Once the cutscene is complete, change to the player camera and start the game.