"""
Generate placeholder audio files and menu background image.
Run from the repo root:  python tools/generate_sounds.py
All files are silent/blank — replace them with real assets.
"""
import os
import struct
import wave
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SOUND_FILES = [
    "train_zoom_off",
    "player_shoot",
    "bullet_hit_damagable",
    "bullet_hit_non_damagable",
    "drone_destroyed",
    "turret_destroyed",
    "drone_deployed",
    "turret_activate",
    "turret_deactivate",
    "container_detached",
    "clamp_destroyed",
    "container_destroyed",
    "player_shield_hit",
    "player_car_hit",
    "cliff_warning",
    "car_accelerating",
    "car_decelerating",
    "ui_container_click",
    "ui_container_open",
    "ui_button_click",
    "ui_options_save",
    "ui_options_back",
    "ui_upgrade_buy",
    "ui_resource_arrive",
]

MUSIC_FILES = [
    "menu_theme",
    "raid_theme",
    "after_action_theme",
]


def make_silent_wav(path, duration_s=0.5, sample_rate=44100):
    num_samples = int(sample_rate * duration_s)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as f:
        f.setnchannels(1)
        f.setsampwidth(2)   # 16-bit
        f.setframerate(sample_rate)
        data = struct.pack("<" + "h" * num_samples, *([0] * num_samples))
        f.writeframes(data)
    print(f"  wrote {path}")


def make_placeholder_png(path, width=1280, height=720, r=15, g=10, b=25):
    """Write a solid-colour PNG without requiring Pillow."""
    os.makedirs(os.path.dirname(path), exist_ok=True)

    def chunk(tag: bytes, data: bytes) -> bytes:
        length = struct.pack(">I", len(data))
        crc = struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        return length + tag + data + crc

    signature = b"\x89PNG\r\n\x1a\n"
    ihdr_data = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    ihdr = chunk(b"IHDR", ihdr_data)

    # Build raw image: one filter byte (0 = None) per row, then RGB pixels
    row = bytes([0] + [r, g, b] * width)
    raw = row * height
    compressed = zlib.compress(raw)
    idat = chunk(b"IDAT", compressed)
    iend = chunk(b"IEND", b"")

    with open(path, "wb") as f:
        f.write(signature + ihdr + idat + iend)
    print(f"  wrote {path}")


if __name__ == "__main__":
    print("Generating placeholder sound files …")
    for name in SOUND_FILES:
        path = os.path.join(ROOT, "assets", "sounds", name + ".wav")
        if not os.path.exists(path):
            make_silent_wav(path, duration_s=0.5)
        else:
            print(f"  skip  {path} (already exists)")

    print("\nGenerating placeholder music files …")
    for name in MUSIC_FILES:
        path = os.path.join(ROOT, "assets", "music", name + ".wav")
        if not os.path.exists(path):
            make_silent_wav(path, duration_s=3.0)
        else:
            print(f"  skip  {path} (already exists)")

    print("\nGenerating placeholder menu background …")
    bg_path = os.path.join(ROOT, "assets", "menu", "background.png")
    if not os.path.exists(bg_path):
        make_placeholder_png(bg_path)
    else:
        print(f"  skip  {bg_path} (already exists)")

    print("\nDone. Replace placeholder files with real assets before shipping.")
