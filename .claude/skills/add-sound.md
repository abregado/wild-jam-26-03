# Skill: add-sound

Use this skill when the user wants to add a new sound effect to the game.

## Steps

### 1. Determine the sound

Ask the user (or infer from context):
- **Sound ID** — the key used in code (e.g. `enemy_spawn`). Snake_case.
- **Filename** — the WAV file the artist will provide (default: same as ID). Can reuse an existing file.
- **Trigger** — which script/event plays the sound.

### 2. Add the WAV placeholder

Run the generator or create the file manually:
```
python tools/generate_sounds.py
```
Or add a one-liner to create a single file:
```python
import wave, struct
path = "assets/sounds/<filename>.wav"
with wave.open(path, "w") as f:
    f.setnchannels(1); f.setsampwidth(2); f.setframerate(44100)
    f.writeframes(struct.pack("<" + "h" * 22050, *([0]*22050)))
```

### 3. Register in game_config.json

Add an entry to `config/game_config.json` under `audio.sounds`:
```json
"<sound_id>": "<filename>"
```
For example:
```json
"enemy_spawn": "enemy_spawn"
```
The value is the filename **without** `.wav`. Two IDs can share one filename.

### 4. No GameConfig.gd change needed

`GameConfig.sounds` is a `Dictionary` that loads all entries automatically.
`SoundManager` loads all streams at startup — no code change needed there either.

### 5. Wire the call

In the script where the sound should play, call:
```gdscript
SoundManager.play("<sound_id>")
```
For looping sounds (engine hum, ambience):
```gdscript
SoundManager.play_loop("<loop_key>", "<sound_id>")  # start
SoundManager.stop_loop("<loop_key>")                 # stop
```

### 6. Volume

Sound volume is controlled globally via:
- `SettingsManager.SetSfxVolume(float)` — live slider
- `game_config.json` → `audio.sfx_volume` — default

No per-sound volume wiring is needed unless the artist requests a relative adjustment.
Add a multiplier field to `game_config.json` (e.g. `"enemy_spawn_volume": 0.8`) and read it in
the callsite if needed: `SoundManager` does not currently support per-sound volume,
so apply it directly to the `AudioStreamPlayer` in a custom callsite.

## Example — adding a "beacon_fire" sound

1. Create `assets/sounds/beacon_fire.wav` (placeholder or real asset)
2. Add to config:
   ```json
   "beacon_fire": "beacon_fire"
   ```
3. In `scripts/projectiles/Beacon.gd`, at the point where the beacon launches:
   ```gdscript
   SoundManager.play("beacon_fire")
   ```
