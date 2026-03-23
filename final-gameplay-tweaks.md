Drones that collider with roof and cliffs should be destroyed.

Containers that are detached are always recovered in the post raid screen, regardless of beacon level.

All containers have to be opened up in the After action screen as if they were not beaconed. Each only need three clicks to open.

Only warn the player of Cliffs approaching using a raycast from the car that is 5*cliff_detection_distance. Just mention a cliff is coming, nothing to do with what kind of obstacle section it is.

Drones have a "min_distance" setting, which forces their next shooting spot to be at least this far away from the player. It selects a position and height, and then if its too close, it instead uses the normalized vector to the player turret times by the min_distance to find the new spot. It is ok if this is outside the min/max height.

Drones should also raycast before they shoot to see if any other drones are in the way. If there are, then they should move instead of shooting.

The player car should have 3 (max_omni_shields) omni-shields that block one hit from any direction. Once no hits on the car, or on the shield collider, have happened for a length of time (shield_cooldown) then it starts recharging a shield point (shield_recharge_time). There should be UI to show how many omni-shields you have ready. 

The directional shield stays as it is now, unlimited. Perhaps we need to shade the shield sphere so that it is slightly less  transparent within shield_block_angle of the crosshair direction. We can have the shield flash based on the hit location and have a ripple of opacity expand out from the hit point, so the player knows where the damage is coming from. If the shot hits the directional shield then this effect does not happen (the player is already looking where the shot came from). If the player has no omni-shields then the shield outside the shield_block_angle should be completely transparent.