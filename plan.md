# GDScript Port Plan

**Goal:** Replace all C# with GDScript to unlock Godot 4 web export → itch.io in-browser play + GitHub Pages.
**Branch:** `gdscript-port` (main stays on C# until this is merged)
**Scope:** 36 `.cs` files → 36 `.gd` files + scene script-path updates

---

## Phase 0 — Project Prep ✅ Done

- [x] Created `gdscript-port` branch
- [x] `project.godot` — removed `[dotnet]` section, removed `"C#"` from `config/features`, updated all 9 autoload paths `.cs` → `.gd`
- [x] `export_presets.cfg` — stripped all `dotnet/` options from all 5 presets
- [x] `.gitignore` — added `/build_web/`
- [x] `release.yml` — removed `setup-dotnet` + `dotnet build` steps
- [x] `pages.yml` — removed `setup-dotnet` + `wasm-tools` + `dotnet build` steps

---

## Translation Reference

| C# | GDScript |
|---|---|
| `List<T>` | `Array` |
| `Dictionary<K,V>` | `Dictionary` |
| `HashSet<T>` | `Dictionary` (use keys only) or `Array` + `.has()` |
| `LINQ .Where(x => ...)` | `for x in arr: if cond: result.append(x)` |
| `LINQ .FirstOrDefault(pred)` | manual loop with `break` + result var |
| `LINQ .OrderBy(x => x.f)` | `arr.sort_custom(func(a,b): return a.f < b.f)` |
| `LINQ .Select(x => x.f)` | `arr.map(func(x): return x.f)` |
| `async Task` / `await ToSignal(obj, "sig")` | `await obj.sig` |
| `static` instance + `static void Foo()` | autoload call `AutoloadName.foo()` |
| `switch (x) { case A: ... }` | `match x:` |
| `x is SomeType y` | `if x is SomeType: var y := x as SomeType` |
| `GetNodeOrNull<T>("path")` | `get_node_or_null("path")` |
| `[Export] public float X` | `@export var x: float` |
| `[Signal] delegate FooEventHandler(...)` | `signal foo(...)` |
| `EmitSignal(SignalName.Foo, arg)` | `foo.emit(arg)` |
| `Connect(SignalName.Foo, Callable.From(cb))` | `foo.connect(cb)` or `foo.connect(func(): ...)` |
| `lambda player.Finished += () => { ... }` | `player.finished.connect(func(): ...)` |
| `TryGetValue(k, out var v)` | `if k in dict: var v = dict[k]` |
| `Mathf.X(...)` | `X(...)` — all built-in (sin, cos, lerp, clamp…) |
| `Mathf.LinearToDb(v)` | `linear_to_db(v)` |
| `Mathf.RadToDeg(r)` | `rad_to_deg(r)` |
| All Godot API methods | snake_case: `MakeCurrent()` → `make_current()` |
| `GD.Randi() % n` | `randi() % n` |
| `GD.Randf()` | `randf()` |
| `GD.Print(...)` | `print(...)` |
| `GD.PrintErr(...)` | `push_error(...)` |
| `GD.Load<T>(path)` | `load(path)` |
| `ResourceLoader.Exists(path)` | `ResourceLoader.exists(path)` |
| `(float)someDouble` | `float(some_double)` — usually not needed in GDScript |
| Inner class `public class Foo { }` | `class Foo:` inside script, or plain Dictionary |
| `DateTime.Now.ToString("yyyy-MM-dd")` | `Time.get_date_string_from_system()` |
| `CreateTween().TweenProperty(...)` | `create_tween().tween_property(...)` |
| `.TweenCallback(Callable.From(() => ...))` | `.tween_callback(func(): ...)` |
| `t.Parallel()` | `t.parallel()` |
| `null!` field initializer | just `= null` or omit — GDScript is nullable by default |

---

## Phase 1 — Autoloads

All 7 must be done before anything else runs. Do in order (each depends on the previous).

### 1a. `GameConfig.gd` (539 lines → ~450 lines)
**Source:** `scripts/autoloads/GameConfig.cs`
**Scene update:** `project.godot` autoload already updated to `.gd` ✅

Key notes:
- Three inner classes at bottom of `.cs` (`CargoType`, `StatModifier`, `UpgradeDefinition`) → declare as `class CargoType:`, `class StatModifier:`, `class UpgradeDefinition:` at top of `.gd` file (GDScript inner classes go at the end, after `_ready`)
- All `{ get; private set; }` properties → plain `var` (no setter needed — GDScript vars are public by default, convention: use `_` prefix for private)
- `TryGetValue` helper pattern: `var t = data.get("turret", null); if t != null:`
- Helper functions `GetFloat`, `GetInt`, `GetBool`, `GetString` → local `func _get_float(d, key, fallback)` etc.
- `List<string>` for music lists → `Array` with `append()` instead of `.Add()`
- `json.Data.AsGodotDictionary()` → `json.data` (already a Dictionary in GDScript)
- `foreach (var kv in dict)` → `for key in dict: var val = dict[key]`
  OR use `dict.keys()` / `dict.values()`
- `Color.HtmlIsValid(hex)` → `Color.html_is_valid(hex)`
- `new Color(hexStr)` → `Color(hex_str)` or `Color.html(hex_str)` if from HTML string
- `ApplyUpgrade(UpgradeDefinition u)` → `func apply_upgrade(u)` — GDScript doesn't type-check the arg but it works fine
- `switch (stat) { case "turret_damage": ... }` → `match stat:`

### 1b. `TrainSpeedManager.gd` (83 lines → ~65 lines)
**Source:** `scripts/autoloads/TrainSpeedManager.cs`

Key notes:
- Very simple, almost 1:1
- `SoundManager.Play("train_zoom_off")` → `SoundManager.play("train_zoom_off")` (autoload call)
- `float.MinValue / 2f` → `-1e38` or `-9999999.0`
- `private float _carSpeedPenalty` → `var _car_speed_penalty: float = 0.0`
- `MaxRelativeForward = float.MinValue / 2f` — this makes the player unable to move forward even at max input. Use `-9999999.0`

### 1c. `GameSession.gd` (123 lines → ~100 lines)
**Source:** `scripts/autoloads/GameSession.cs`

Key notes:
- Signals: `[Signal] delegate CargoCollectedEventHandler(string cargoName)` → `signal cargo_collected(cargo_name: String)`
- `[Signal] delegate StatsChangedEventHandler()` → `signal stats_changed`
- `EmitSignal(SignalName.CargoCollected, cargoName)` → `cargo_collected.emit(cargo_name)`
- `EmitSignal(SignalName.StatsChanged)` → `stats_changed.emit()`
- `LoadFromSave(SaveManager.SlotData data, int slot)` → `func load_from_save(data: Dictionary, slot: int)` — `SlotData` becomes a plain Dictionary
- `FirstOrDefault(u => u.Id == id)` → manual loop:
  ```gdscript
  var def = null
  for u in GameConfig.upgrades:
      if u.id == id:
          def = u
          break
  ```
- `new Dictionary<string, int>(data.Resources)` → `data.resources.duplicate()`
- `new List<string>(data.Upgrades)` → `data.upgrades.duplicate()`

### 1d. `SaveManager.gd` (106 lines → ~90 lines)
**Source:** `scripts/autoloads/SaveManager.cs`

Key notes:
- Inner class `SlotData` → removed; `load_slot()` returns a Dictionary with keys `raids_played`, `last_save`, `resources`, `upgrades` (or `null` on missing/error)
- `DirAccess.MakeDirRecursiveAbsolute(SaveDir)` → `DirAccess.make_dir_recursive_absolute(save_dir)`
- `FileAccess.FileExists(path)` → `FileAccess.file_exists(path)`
- `using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)` → `var file := FileAccess.open(path, FileAccess.READ)`
- `file.GetAsText()` → `file.get_as_text()`
- `json.Data.AsGodotDictionary()` → `json.data` (already Dictionary)
- `DateTime.Now.ToString("yyyy-MM-dd")` → `Time.get_date_string_from_system()`
- `Json.Stringify(data)` → `JSON.stringify(data)`
- `file.StoreString(...)` → `file.store_string(...)`
- `DirAccess.RemoveAbsolute(path)` → `DirAccess.remove_absolute(path)`
- Tuple `(int raids, string date)` return → return `[raids, date]` Array

### 1e. `SoundManager.gd` (122 lines → ~100 lines)
**Source:** `scripts/autoloads/SoundManager.cs`

Key notes:
- Static API pattern: Since `SoundManager` is an autoload, callers already do `SoundManager.play(id)` — no `_instance` needed, just plain methods on the autoload node
- Remove `_instance` entirely, remove all `_instance?.X()` indirection — all methods are direct
- `private AudioStreamPlayer[] _pool` → `var _pool: Array[AudioStreamPlayer] = []`
- `HashSet<string> _activeLoops` → `var _active_loops: Dictionary = {}` (key = loop key, value = true)
- `Dictionary<string, AudioStreamPlayer> _loops` → `var _loops: Dictionary = {}`
- `Dictionary<string, AudioStream> _streams` → `var _streams: Dictionary = {}`
- `foreach (var kv in config.Sounds)` → `for key in GameConfig.sounds: var val = GameConfig.sounds[key]`
- `player.Finished += () => { if (_activeLoops.Contains(capturedKey)...) }` →
  ```gdscript
  player.finished.connect(func():
      if captured_key in _active_loops and captured_key in _loops:
          _loops[captured_key].play()
  )
  ```
- Lambda captures: GDScript closures capture by reference — use a local variable assigned before `func():` to capture the key value

### 1f. `MusicManager.gd` (116 lines → ~95 lines)
**Source:** `scripts/autoloads/MusicManager.cs`

Key notes:
- Like SoundManager: remove `_instance`, all methods are direct autoload methods
- `Dictionary<string, List<AudioStream>> _trackLists` → `var _track_lists: Dictionary = {}`
- `_aIsActive` bool + `ActivePlayer` / `NextPlayer` properties → `var _a_is_active: bool = true` + local helper `func _active_player()` / `func _next_player()`
- `foreach (var ext in new[] { ".ogg", ".mp3", ".wav" })` →
  ```gdscript
  for ext in [".ogg", ".mp3", ".wav"]:
  ```
- `tracks[(int)(GD.Randi() % (uint)tracks.Count)]` → `tracks[randi() % tracks.size()]`
- Tween crossfade:
  ```gdscript
  var t := create_tween()
  t.tween_property(_active_player(), "volume_db", -80.0, crossfade_time).set_trans(Tween.TRANS_SINE)
  t.parallel().tween_property(next, "volume_db", 0.0, crossfade_time).set_trans(Tween.TRANS_SINE)
  t.tween_callback(func():
      _active_player().stop()
      _a_is_active = !_a_is_active
  )
  ```
- Note: `_active_player()` is called at tween creation, not when callback fires. The callback should capture references directly.

### 1g. `SettingsManager.gd` (219 lines → ~180 lines)
**Source:** `scripts/autoloads/SettingsManager.cs`

Key notes:
- `static readonly (string Label, string Action)[]` tuple array → `const REBINDABLE_ACTIONS = [["Move Forward", "move_forward"], ...]`
- `ev is InputEventKey key` type check:
  ```gdscript
  if ev is InputEventKey:
      var key := ev as InputEventKey
      var k = key.physical_keycode if key.physical_keycode != KEY_NONE else key.keycode
      return OS.get_keycode_string(k)
  ```
- `mb.ButtonIndex switch` → `match mb.button_index:`
- `AudioServer.GetBusIndex(name)` → `AudioServer.get_bus_index(name)`
- `AudioServer.BusCount` → `AudioServer.get_bus_count()`
- `AudioServer.AddBus(idx)` → `AudioServer.add_bus(idx)`
- `AudioServer.SetBusName(idx, name)` → `AudioServer.set_bus_name(idx, name)`
- `AudioServer.SetBusSend(idx, "Master")` → `AudioServer.set_bus_send(idx, "Master")`
- `AudioServer.SetBusVolumeDb(...)` → `AudioServer.set_bus_volume_db(...)`
- `AudioServer.SetBusMute(...)` → `AudioServer.set_bus_mute(...)`
- `Mathf.LinearToDb(v)` → `linear_to_db(v)`
- `InputMap.ActionGetEvents(action)` → `InputMap.action_get_events(action)`
- `InputMap.ActionEraseEvents(action)` → `InputMap.action_erase_events(action)`
- `InputMap.ActionAddEvent(action, ev)` → `InputMap.action_add_event(action, ev)`
- `new InputEventKey { PhysicalKeycode = (Key)pcV.AsInt32() }` →
  ```gdscript
  var new_event := InputEventKey.new()
  new_event.physical_keycode = int(pc_v)
  ```

---

## Phase 2 — Leaf Scripts

No custom script dependencies. Do in any order.

### `Shield.gd` (122 lines → ~100 lines)
**Source:** `scripts/player/Shield.cs`
**Scene:** `PlayerCar.tscn` or built at runtime inside `PlayerCar._ready()`

Key notes:
- `GetParent().GetNode<Camera3D>("Camera3D")` → `get_parent().get_node("Camera3D")`
- `BaseMaterial3D.TransparencyEnum.Alpha` → `BaseMaterial3D.TRANSPARENCY_ALPHA`
- `BaseMaterial3D.ShadingModeEnum.Unshaded` → `BaseMaterial3D.SHADING_MODE_UNSHADED`
- `BaseMaterial3D.CullModeEnum.Disabled` → `BaseMaterial3D.CULL_DISABLED`
- `area.AreaEntered += OnAreaEntered` → `area.area_entered.connect(_on_area_entered)`
- `if (other.GetParent() is not DroneBullet bullet)` → `if not other.get_parent() is DroneBullet: return`
- `_camera.GlobalTransform.Basis.Z` → `_camera.global_transform.basis.z`
- `Mathf.RadToDeg(Mathf.Acos(dot))` → `rad_to_deg(acos(dot))`
- `SoundManager.Play(...)` → `SoundManager.play(...)`
- `VfxSpawner.Spawn(...)` → `VfxSpawner.spawn(...)`

### `Beacon.gd` (55 lines → ~45 lines)
**Source:** `scripts/projectiles/Beacon.cs`
**Scene:** `scenes/projectiles/Beacon.tscn` — update script path

Key notes:
- Straightforward port — no complex patterns
- `area.AreaEntered += OnAreaEntered` → `area.area_entered.connect(_on_area_entered)`
- `GlobalPosition += -GlobalTransform.Basis.Z * move` → `global_position += -global_transform.basis.z * move`
- `if parent is ContainerNode container` → `if parent is ContainerNode: var container := parent as ContainerNode`

### `DroneBullet.gd` (~97 lines → ~80 lines)
**Source:** find at `scripts/enemies/DroneBullet.cs` (not found at first path — check actual location)

### `RingIndicator.gd` (53 lines → ~45 lines)
**Source:** `scripts/CutsceneManager.cs` references `RingIndicator` — find actual file

### `Carriage.gd` (81 lines → ~65 lines)
**Source:** `scripts/train/Carriage.cs`

### `DayNightCycle.gd` (132 lines → ~110 lines)
**Source:** `scripts/environment/DayNightCycle.cs`

Key notes:
- `static readonly Color` constants → `const SUN_COLOR_NOON = Color(1.0, 0.92, 0.78)` etc.
- `ProceduralSkyMaterial? _skyMat` → `var _sky_mat: ProceduralSkyMaterial = null`
- `_env.Sky?.SkyMaterial as ProceduralSkyMaterial` → `_env.sky.sky_material if _env.sky else null`
- `_sun.RotationDegrees = new Vector3(elev, SunAzimuth, 0f)` → `_sun.rotation_degrees = Vector3(elev, SUN_AZIMUTH, 0.0)`
- All Color lerp: `SunColorNoon.Lerp(SunColorSunset, phase0)` → `SUN_COLOR_NOON.lerp(SUN_COLOR_SUNSET, phase0)`

### `CloudPool.gd`, `RubblePool.gd`, `RockPillarPool.gd`, `PillarPool.gd`
**Sources:** `scripts/environment/`

Key notes (common to all pools):
- All use `RandomNumberGenerator` → replace with global `randf()`, `randf_range()`, `randi_range()`
- `_rng.RandfRange(a, b)` → `randf_range(a, b)`
- `GetNode<MeshInstance3D>(...)` → `get_node(...) as MeshInstance3D`
- GLB loading: `GD.Load<PackedScene>(path)` → `load(path) as PackedScene`

### `VfxSpawner.gd` (136 lines → ~110 lines)
**Source:** `scripts/autoloads/VfxSpawner.cs`

Key notes:
- `static VfxSpawner Instance` → removed; autoload is accessed as `VfxSpawner` directly
- `public static void Spawn(...)` → `func spawn(id: String, world_pos: Vector3)` (static removed — autoload call)
- Inner `struct EffectConfig` → use a Dictionary `{"color": ..., "size": ..., ...}` or inner class
- `static readonly Dictionary<string, EffectConfig> Fallbacks` → `const FALLBACKS: Dictionary = {...}`
- `_checkedPaths: HashSet<string>` → `var _checked_paths: Dictionary = {}`
- `timer.Timeout += root.QueueFree` → `timer.timeout.connect(root.queue_free)`

---

## Phase 3 — Mid-Level Systems

### `ContainerNode.gd` (188 lines)
**Source:** `scripts/train/ContainerNode.cs`

Key notes:
- Has signals: check for `[Signal]` declarations → convert to `signal`
- Tween callbacks → `tween_callback(func(): ...)`

### `ClampNode.gd` (180 lines)
**Source:** `scripts/train/ClampNode.cs`

Key notes:
- Switch expressions for orientation logic → `match`

### `Bullet.gd` (188 lines)
**Source:** `scripts/projectiles/Bullet.cs`

Key notes:
- `PhysicsRayQueryParameters3D` → `PhysicsRayQueryParameters3D.create(from, to)` then `get_world_3d().direct_space_state.intersect_ray(query)`
- `Dictionary result checking` → `if result.size() > 0:`
- Per-frame raycast in `_process`

### `DroneNode.gd` (389 lines)
**Source:** `scripts/enemies/DroneNode.cs`

Key notes:
- Enum state machine `enum DroneState { Deploying, Combat, Returning, Dead }` → `enum DroneState { DEPLOYING, COMBAT, RETURNING, DEAD }`
- `match _state:` for state dispatch

### `DeployerNode.gd` (129 lines)
**Source:** `scripts/enemies/DeployerNode.cs`

### `RoofTurretNode.gd` (428 lines)
**Source:** `scripts/enemies/RoofTurretNode.cs`

Key notes:
- State machine with tween chains
- `enum TurretState` → `enum TurretState { ... }`

### `PlayerCar.gd` (357 lines)
**Source:** `scripts/player/PlayerCar.cs`

Key notes:
- `PhysicsShapeQueryParameters3D` → `PhysicsShapeQueryParameters3D.new()` then set properties
- `Input.IsActionPressed("move_forward")` → `Input.is_action_pressed("move_forward")`
- Shield built at runtime — `var _shield := Shield.new()` ... must ensure Shield.gd is a Node3D subclass

### `Turret.gd` (306 lines)
**Source:** `scripts/player/Turret.cs`

Key notes:
- Quaternion slerp: `Quaternion.Slerp(a, b, t)` → `a.slerp(b, t)`
- Raycast hits: `PhysicsRayQueryParameters3D`

### `TrackEnvironment.gd` (103 lines)
**Source:** `scripts/environment/TrackEnvironment.cs`

### `ObstacleManager.gd` (124 lines)
**Source:** `scripts/environment/ObstacleManager.cs`

Key notes:
- Enum state machine → `enum`
- Tuple return `(cliff, limit)` → return Array `[cliff, limit]` from roll function

### `ObstaclePool.gd` (435 lines)
**Source:** `scripts/environment/ObstaclePool.cs`

Key notes:
- Heaviest use of switch expressions with pattern matching in enum
- `Zone switch { ZoneType.Cliff => ..., ZoneType.Limit => ... }` → `match zone:`
- 4 zones array management
- `out sx, sy, sz` parameters → return Array `[sx, sy, sz]` from helper function

---

## Phase 4 — High-Level Systems

### `TrainBuilder.gd` (437 lines)
**Source:** `scripts/train/TrainBuilder.cs`

Key notes — signal wiring (must match exactly):
```
# These signals wire in TrainBuilder._ready():
container.cargo_detached.connect(GameSession.on_cargo_detached)
container.cargo_detached.connect(TrainSpeedManager.on_container_detached)
container.container_destroyed.connect(GameSession.on_container_destroyed)
```
- `new RandomNumberGenerator()` → use global `randi_range()` / `randf_range()`
- Lambda signal wiring: `container.CargoDetached += session.OnCargoDetached` → `container.cargo_detached.connect(session.on_cargo_detached)`

### `LevelManager.gd` (163 lines)
**Source:** `scripts/LevelManager.cs`

### `HUD.gd` (257 lines)
**Source:** `scripts/ui/HUD.cs`

Key notes:
- Dynamic UI construction from code → same in GDScript
- `InputMap.ActionGetEvents(action)` → `InputMap.action_get_events(action)`

### `Main.gd` (115 lines)
**Source:** `scripts/Main.cs`

---

## Phase 5 — Complex UI & Cutscene

### `MainMenu.gd` (285 lines)
**Source:** `scripts/ui/MainMenu.cs`

Key notes:
- `SaveManager.SlotData` type removed → use Dictionary from `SaveManager.load_slot(n)`
- Button callbacks with closures → `button.pressed.connect(func(): ...)`

### `OptionsMenu.gd` (280 lines)
**Source:** `scripts/ui/OptionsMenu.cs`

Key notes:
- Snapshot pattern for cancel: save state dict before opening, restore on cancel
- `ev is InputEventKey` → GDScript `is` check

### `AfterAction.gd` (784 lines) ⚠️ Largest file
**Source:** `scripts/ui/AfterAction.cs`

Key notes:
- Complex multi-phase state machine
- LINQ throughout: `.Where()`, `.OrderBy()`, `.Take()` → manual loops
- 3D SubViewport for upgrade card flip — same API in GDScript
- Deferred callbacks: `CallDeferred(...)` → `call_deferred("method_name")` or `func(): ...` with `await get_tree().process_frame`
- `async` methods: each `async` sub-method → either inline or use `await` at call sites

### `CutsceneManager.gd` (454 lines) ⚠️ Most async complexity
**Source:** `scripts/CutsceneManager.cs`

Key notes:
- `async Task PlayCutscene()` → `func play_cutscene():` with `await` throughout (GDScript 4 supports `await` natively)
- `await ToSignal(GetTree().CreateTimer(t), "timeout")` → `await get_tree().create_timer(t).timeout`
- `await ToSignal(tween, "finished")` → `await tween.finished`
- `_advanceRequested` bool for skip: set in `_unhandled_input`, checked between awaits
- LINQ waypoint sorting: `.Where(...).OrderByDescending(n => n.GlobalPosition.Z).ToList()` → manual filter + sort
- `_cam.MakeCurrent()` → `_cam.make_current()`
- UI construction (Panel, Label, etc.) → same in GDScript, just snake_case methods

---

## Phase 6 — CI & Web Export

After Phase 5 is verified working in-editor:

1. **`release.yml`** — Add Web export + itch.io butler upload after existing Windows/Linux/Mac exports
   - Requires repo secrets: `BUTLER_API_KEY`, `ITCH_GAME` (format: `username/game-slug`)
2. **`pages.yml`** — Re-enable web export step (already clean, no wasm-tools needed)
3. **`export_presets.cfg`** — Web preset already added and clean ✅
4. **GitHub repo settings** — Enable Pages source: "GitHub Actions"
5. **itch.io** — Create game page, set to "downloadable", grab butler API key

---

## Scene File Updates Required

After each `.gd` file is written, update the corresponding `.tscn` to point to the new script:

```
# Old
[ext_resource type="Script" path="res://scripts/foo/Bar.cs" id="..."]
# New
[ext_resource type="Script" path="res://scripts/foo/Bar.gd" id="..."]
```

This can be done in the Godot editor (drag new script onto the node) or by sed on the `.tscn` file.
**Do scene updates together with each phase, not all at the end.**

---

## Verification Checkpoints

| After Phase | Check |
|---|---|
| 1 (Autoloads) | Open Godot editor — no autoload errors in Output |
| 2 (Leaves) | Godot editor opens without missing-script errors on leaf nodes |
| 3 (Mid) | Press Play — game runs, player can move, enemies spawn |
| 4 (High) | Full gameplay loop works: raid starts, level ends, AfterAction loads |
| 5 (UI) | Main menu works, cutscene plays, options save/load, upgrades purchase |
| 6 (CI) | `godot --headless --export-release "Web" /tmp/test/index.html` succeeds locally |

---

## Files To Port (Checklist)

### Phase 1 — Autoloads
- [x] `scripts/autoloads/GameConfig.cs` → `GameConfig.gd`
- [x] `scripts/autoloads/TrainSpeedManager.cs` → `TrainSpeedManager.gd`
- [x] `scripts/autoloads/GameSession.cs` → `GameSession.gd`
- [x] `scripts/autoloads/SaveManager.cs` → `SaveManager.gd`
- [x] `scripts/autoloads/SoundManager.cs` → `SoundManager.gd`
- [x] `scripts/autoloads/MusicManager.cs` → `MusicManager.gd`
- [x] `scripts/autoloads/SettingsManager.cs` → `SettingsManager.gd`

### Phase 2 — Leaves
- [x] `scripts/player/Shield.cs` → `Shield.gd`
- [x] `scripts/projectiles/Beacon.cs` → `Beacon.gd`
- [x] `scripts/enemies/DroneBullet.cs` → `DroneBullet.gd`
- [x] `scripts/ui/RingIndicator.cs` → `RingIndicator.gd`
- [x] `scripts/train/Carriage.cs` → `Carriage.gd`
- [x] `scripts/environment/DayNightCycle.cs` → `DayNightCycle.gd`
- [x] `scripts/environment/CloudPool.cs` → `CloudPool.gd`
- [x] `scripts/environment/RubblePool.cs` → `RubblePool.gd`
- [x] `scripts/environment/RockPillarPool.cs` → `RockPillarPool.gd`
- [x] `scripts/environment/PillarPool.cs` → `PillarPool.gd`
- [x] `scripts/autoloads/VfxSpawner.cs` → `VfxSpawner.gd`

### Phase 3 — Mid-Level ✅ Done
- [x] `scripts/train/ContainerNode.cs` → `ContainerNode.gd`
- [x] `scripts/train/ClampNode.cs` → `ClampNode.gd`
- [x] `scripts/projectiles/Bullet.cs` → `Bullet.gd`
- [x] `scripts/enemies/DroneNode.cs` → `DroneNode.gd`
- [x] `scripts/enemies/DeployerNode.cs` → `DeployerNode.gd`
- [x] `scripts/enemies/RoofTurretNode.cs` → `RoofTurretNode.gd`
- [x] `scripts/player/PlayerCar.cs` → `PlayerCar.gd`
- [x] `scripts/player/Turret.cs` → `Turret.gd`
- [x] `scripts/environment/TrackEnvironment.cs` → `TrackEnvironment.gd`
- [x] `scripts/environment/ObstacleManager.cs` → `ObstacleManager.gd` (placed in autoloads/)
- [x] `scripts/environment/ObstaclePool.cs` → `ObstaclePool.gd`

### Phase 4 — High-Level ✅ Done
- [x] `scripts/train/TrainBuilder.cs` → `TrainBuilder.gd`
- [x] `scripts/LevelManager.cs` → `LevelManager.gd`
- [x] `scripts/ui/HUD.cs` → `HUD.gd`
- [x] `scripts/Main.cs` → `Main.gd`

### Phase 5 — Complex UI & Cutscene
- [ ] `scripts/ui/MainMenu.cs` → `MainMenu.gd`
- [ ] `scripts/ui/OptionsMenu.cs` → `OptionsMenu.gd`
- [ ] `scripts/ui/AfterAction.cs` → `AfterAction.gd`
- [ ] `scripts/CutsceneManager.cs` → `CutsceneManager.gd`
