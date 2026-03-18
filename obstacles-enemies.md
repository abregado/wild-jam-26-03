
Obstacles sections for the car. These are spawned like the pillars and move backwards until they are removed. Which obstacle is spawning changes at random intervals during the level. While one obstacle is spawning, instances of the obstacle spawn constantly so the area is blocked completely. The car cannot actually collide with the obstacles. In the case of the cliffs, if one is approaching then the car automatically changes side via a route they are allowed to use. We need to detect which sections are coming so we can disable the appropriate side changing abilities before the player would collide.

An obstacle is made up of two values
Cliff Side. Can only be left or right or none. The entire side is filled by the cliff objects. cliff objects are random in height when they spawn but always are higher than the top of the arc used to move over the train. Only one side can be active (or none) at a time.

Movement Limitation. Either a rocky roof above the train, or a rocky plataeu below the train, or none. The roof is low enough that the player would not be able to change sides over the top, and not high enough that drones can deploy. Both these features are disabled why having a roofed obstacle section. The rocky plateu goes up to the rail, effectivly coving most of the pillars. The player cannot change sides under during this time.

each obstacle section has both settings, so it is possible to have a clear section, or a section with both left cliff and roof, ect.

Each section is made of brown cube objects that are spawned ahead of the train and then despawned behind it. Their speed is also based on the train speed. Use a few different desert like shades of brown to vary the look. Make the heights different for each object so it isnt just flat. As long as the heights dont interfere with the other models.