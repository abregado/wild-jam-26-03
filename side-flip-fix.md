There are many problems with the side flipping system, so lets make a plan to refactor it.

The player should be able to flip over or under at any time, unless the flip path intersects with any obstacle geometry. Use multiple raycasts along the trajectory, factoring in current move speed to see if it is allowed. 

While flipping over or under, the player cannot control their speed. They stay at the speed they were going when they flipped. This way we can use the speed to calculate if the car will clip any obstacle geometry on the way.

The Car can now go through pillars without issue. Pillars do not effect Flipping in any way.

The car is also shooting a ray forward to detect oncoming cliff obstacles. If one is detected then it checks both Flipping Over and Under paths to see if one is available. If none are available, it slows the car a bit for the next frame. If one is available, it Flips automatically.

To assist with making it possible to Flip between sections, the first Roof obstacle in any section does not spawn, and the first Plateu obstacle in every section is half height.

While completing a Flip, check ahead of the car in the direction of the flip trajectory. If an Obstacle is detected, then Stop this flip and flip back from here (follow the reverse path). This should not be needed if we are checking the whole Flip path before starting, but do it anyway.

The Flip over and under button prompts should just be active all the time.