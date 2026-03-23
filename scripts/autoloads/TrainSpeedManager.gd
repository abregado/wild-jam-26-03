## Autoload singleton. Central speed authority. All systems read current_train_speed from here.
##
## Speed math:
##   current_train_speed starts at base_train_speed.
##   Each cargo detach: current_train_speed += speed_increase_per_container.
##   max_relative_forward = config_max - (current_train_speed - base_train_speed)
##   When max_relative_forward goes negative, player drifts backward even at full forward.
##
## TrackEnvironment polls current_train_speed each _process frame.

extends Node

var current_train_speed: float = 0.0
var train_zoom_speed: float = 0.0
var max_relative_forward: float = 0.0
var max_relative_backward: float = 0.0

var _is_zooming_away: bool = false
var _car_speed_penalty: float = 0.0


func _ready() -> void:
	reset_speed()


func reset_speed() -> void:
	_is_zooming_away = false
	_car_speed_penalty = 0.0
	current_train_speed = GameConfig.base_train_speed
	max_relative_forward = GameConfig.max_relative_velocity
	max_relative_backward = GameConfig.min_relative_velocity


## Called when a drone bullet hits the player car. Reduces max forward velocity.
func apply_car_speed_damage(amount: float) -> void:
	_car_speed_penalty += amount
	var speed_increase := current_train_speed - GameConfig.base_train_speed
	max_relative_forward = GameConfig.max_relative_velocity - speed_increase - _car_speed_penalty


## Called when a container detaches (cargo collected).
func on_container_detached() -> void:
	_apply_speed_increase()


## Called when a container is destroyed (cargo lost).
func on_container_destroyed() -> void:
	_apply_speed_increase()


func _apply_speed_increase() -> void:
	if _is_zooming_away:
		return
	current_train_speed += GameConfig.speed_increase_per_container
	var speed_increase := current_train_speed - GameConfig.base_train_speed
	max_relative_forward = GameConfig.max_relative_velocity - speed_increase - _car_speed_penalty


## Returns true if the player is too far behind the train to use their turret.
func is_player_out_of_range(player_z: float, train_front_z: float) -> bool:
	return (train_front_z - player_z) > GameConfig.turret_range


## Called by LevelManager when the countdown expires. Zooms the train away.
func trigger_zoom_away() -> void:
	_is_zooming_away = true
	train_zoom_speed = current_train_speed * 10.0
	current_train_speed = 0.0
	max_relative_forward = -9999999.0
	SoundManager.play("train_zoom_off")
	print("[TrainSpeedManager] Zoom away triggered!")
