Each different kind of clamp needs its own model.

We need to replace the materials that come with the imported .glb files with the colored material we are using for the placeholders. We wont have time in this project to add textures to the glbs, so lets use the plain color materials on the imported files.

Drones need to face the way they are moving, and to face toward the player when shooting, but they should not tilt, only rotating on the y axis.

Drones should scale up with a punchy tween when bring deployed from a deployer.

The roof turret needs a multipart model. Its base, the turret dome and the turret barrel. The barrel should point at the player. If we can have these all in one glb file, that would be good.

The turret starts with the turret dome flipped over so that the dome is inside the base. When it goes to active, the dome rotates over, and the barrels scale up so they extend from the dome. The reverse happens when it goes inactive. If destroyed, we make the dome and barrels invisible and play an explosion particle effect.  

Carriages currently get scaled based on how many containers they have. Instead of scaling, we should have one model for carriages that are one container long, another for carriages with two containers, and for three another. Select the mesh that is appropriate for the number of containers attached per side.

Pillars should be a little shorter. Currently, they stick up through the track a little bit.

The player car model will be used for the body, and the current turret model is used for the barrels. We also need a model for the turret dome itself where the barrels are connected. This model can be invisible during play, but is necessary due to the cutscenes.

We need an easy way for the artist to modify particle effects. For each place where we want to have particle effect, there should be a reference to spawn a scene containing a particle effects node. These need to be documented somewhere, so the artist can replace them easily. We also need a placeholder scene for each, with some basic particles.

There should be particle effects for:
player bullet hits something damageable
player bullet hits something non-damageable
clamp is destroyed
turret put into repair mode
drone deployed
drone destroyed
container detaches from the train
enemy bullet hits shield
enemy bullet hits car
drone preparing to fire
drone muzzle flash
turret preparing to fire
turret muzzle flash
player turret muzzle flash

it would be good to have a day/night cycle. The sun should start high and go down after 4 minutes of playing. The sun starts high at the beginning of each raid. The day night cycle length, and if it is enabled should be available in the config file. 

Roof obstacles need to have some vertical supports on both sides when they are in a section without cliffs. These should be far enough away from the train that the car can pass by without touching them. They should have colliders anyway because bullets should be destroyed when they hit them. We might need to increase the width of roof objects to make this work.