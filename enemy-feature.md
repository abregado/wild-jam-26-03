The first enemy in the game are flying drones which fire at the player. The drones deploy from objects on top of the carriages called Deployers. When they deploy, drones first fly up a few units, then select a position above and near to the car and fly there. Deployers only start deploying drones once the player has damaged clamps or containers on its carriage or neighbouring carriages. Once activated, Deployers will deploy up to their maximum drones on a cooldown. If they are at max drones, they do not spawn new drones. If one of their drones is destroyed the Deployers cooldown is maxed, and when finished, it spawns a new drone.

Drones only fire once they are in position. After each shot, there is a chance that they will choose a new location and fly there before shooting again. Drones can be shot and destroyed by the players bullets. When a drone dies, it falls to the ground and then disappears.

When a Drone fires, it uses the hit chance to determine if it fires at a random spot that will hit the car, or a random spot that will narrowly miss the car. If a drone bullet hits the car, then the cars maximum speed is reduced a tiny amount. Drone bullets cannot hit the turret.

The player can block drone bullets with a shield. The shield is a transparent sphere around the car. If a bullet hits the shield and the hit point is within an angle of where the camera is looking, then it is destroyed and the shield flashes. If the camera look angle is not close enough to where the bullet hits, it passes through the shield without effect (no flash).

If the player changes sides of the train, then the drones will follow the player by flying over the top of the train and then selecting a new firing position.

Drones, Deployers and bullets need a model.

new settings for the config file:
Max drones per deployer
max Deployers per carriage
Deployer cooldown time
Drone non-combat move speed (while moving from deployer to the car)
Drone combat move speed (while repositioning after a shot)
Drone fire rate
Car Speed damage per hit
Drone height above the car min/max
Drone hitpoints
Drone bullet speed
Drone bullet size
Drone bullet hit chance