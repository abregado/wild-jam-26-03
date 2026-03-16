using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Loads game_config.json on _Ready and exposes typed properties.
/// Collision layers: 1=World, 2=Containers, 3=Clamps, 4=Player, 5=Projectiles
/// </summary>
public partial class GameConfig : Node
{
    // Turret
    public float TurretDamage { get; private set; } = 25f;
    public float RateOfFire { get; private set; } = 3f;
    public float BlastRadius { get; private set; } = 2.5f;
    public float BulletSpeed { get; private set; } = 60f;
    public int AmmoPerClip { get; private set; } = 10;
    public float ReloadTime { get; private set; } = 2f;

    // Beacon
    public float BeaconReloadSpeed { get; private set; } = 1.5f;
    public float BeaconSpeed { get; private set; } = 25f;

    // Train
    public int MinCarriages { get; private set; } = 3;
    public int MaxCarriages { get; private set; } = 6;
    public int MinContainersPerCarriage { get; private set; } = 1;
    public int MaxContainersPerCarriage { get; private set; } = 3;
    public int MinClampsPerContainer { get; private set; } = 2;
    public int MaxClampsPerContainer { get; private set; } = 4;

    // Clamps / Containers
    public float ClampHitpoints { get; private set; } = 50f;
    public float ContainerHitpoints { get; private set; } = 150f;

    // Player
    public float MinRelativeVelocity { get; private set; } = -15f;
    public float MaxRelativeVelocity { get; private set; } = 5f;
    public float CarAcceleration { get; private set; } = 5f;
    public float CarDeceleration { get; private set; } = 8f;

    // Speed
    public float BaseTrainSpeed { get; private set; } = 15f;
    public float SpeedIncreasePerContainer { get; private set; } = 2f;
    public float TurretRange { get; private set; } = 50f;

    // Cargo Types
    public List<CargoType> CargoTypes { get; private set; } = new();

    public override void _Ready()
    {
        LoadConfig();
        GD.Print($"[GameConfig] Loaded. Carriages: {MinCarriages}-{MaxCarriages}, " +
                 $"BaseSpeed: {BaseTrainSpeed}, TurretRange: {TurretRange}");
    }

    private void LoadConfig()
    {
        const string path = "res://config/game_config.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr("[GameConfig] Config file not found, using defaults.");
            SetupDefaultCargo();
            return;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var jsonText = file.GetAsText();

        var json = new Json();
        if (json.Parse(jsonText) != Error.Ok)
        {
            GD.PrintErr($"[GameConfig] JSON parse error: {json.GetErrorMessage()}");
            SetupDefaultCargo();
            return;
        }

        var data = json.Data.AsGodotDictionary();

        if (data.TryGetValue("turret", out var turretVar))
        {
            var t = turretVar.AsGodotDictionary();
            TurretDamage = GetFloat(t, "damage", TurretDamage);
            RateOfFire = GetFloat(t, "rate_of_fire", RateOfFire);
            BlastRadius = GetFloat(t, "blast_radius", BlastRadius);
            BulletSpeed = GetFloat(t, "bullet_speed", BulletSpeed);
            AmmoPerClip = GetInt(t, "ammo_per_clip", AmmoPerClip);
            ReloadTime = GetFloat(t, "reload_time", ReloadTime);
        }

        if (data.TryGetValue("beacon", out var beaconVar))
        {
            var b = beaconVar.AsGodotDictionary();
            BeaconReloadSpeed = GetFloat(b, "beacon_reload_speed", BeaconReloadSpeed);
            BeaconSpeed = GetFloat(b, "beacon_speed", BeaconSpeed);
        }

        if (data.TryGetValue("train", out var trainVar))
        {
            var tr = trainVar.AsGodotDictionary();
            MinCarriages = GetInt(tr, "min_carriages", MinCarriages);
            MaxCarriages = GetInt(tr, "max_carriages", MaxCarriages);
            MinContainersPerCarriage = GetInt(tr, "min_containers_per_carriage", MinContainersPerCarriage);
            MaxContainersPerCarriage = GetInt(tr, "max_containers_per_carriage", MaxContainersPerCarriage);
            MinClampsPerContainer = GetInt(tr, "min_clamps_per_container", MinClampsPerContainer);
            MaxClampsPerContainer = GetInt(tr, "max_clamps_per_container", MaxClampsPerContainer);
        }

        if (data.TryGetValue("clamps", out var clampsVar))
        {
            var c = clampsVar.AsGodotDictionary();
            ClampHitpoints = GetFloat(c, "clamp_hitpoints", ClampHitpoints);
        }

        if (data.TryGetValue("containers", out var containersVar))
        {
            var c = containersVar.AsGodotDictionary();
            ContainerHitpoints = GetFloat(c, "container_hitpoints", ContainerHitpoints);
        }

        if (data.TryGetValue("player", out var playerVar))
        {
            var p = playerVar.AsGodotDictionary();
            MinRelativeVelocity = GetFloat(p, "min_relative_velocity", MinRelativeVelocity);
            MaxRelativeVelocity = GetFloat(p, "max_relative_velocity", MaxRelativeVelocity);
            CarAcceleration = GetFloat(p, "car_acceleration", CarAcceleration);
            CarDeceleration = GetFloat(p, "car_deceleration", CarDeceleration);
        }

        if (data.TryGetValue("speed", out var speedVar))
        {
            var s = speedVar.AsGodotDictionary();
            BaseTrainSpeed = GetFloat(s, "base_train_speed", BaseTrainSpeed);
            SpeedIncreasePerContainer = GetFloat(s, "speed_increase_per_container", SpeedIncreasePerContainer);
            TurretRange = GetFloat(s, "turret_range", TurretRange);
        }

        if (data.TryGetValue("cargo_types", out var cargoVar))
        {
            CargoTypes.Clear();
            var arr = cargoVar.AsGodotArray();
            foreach (var item in arr)
            {
                var d = item.AsGodotDictionary();
                var name = d["name"].AsString();
                var colorHex = d["color"].AsString();
                if (Color.HtmlIsValid(colorHex))
                    CargoTypes.Add(new CargoType { Name = name, Color = new Color(colorHex) });
            }
        }

        if (CargoTypes.Count == 0)
            SetupDefaultCargo();
    }

    private void SetupDefaultCargo()
    {
        CargoTypes = new List<CargoType>
        {
            new() { Name = "Electronics", Color = new Color("#4488FF") },
            new() { Name = "Chemicals",   Color = new Color("#44FF88") },
            new() { Name = "Weapons",     Color = new Color("#FF4444") },
            new() { Name = "Credits",     Color = new Color("#FFCC00") },
        };
    }

    private static float GetFloat(Godot.Collections.Dictionary d, string key, float fallback)
        => d.TryGetValue(key, out var v) ? (float)v.AsDouble() : fallback;

    private static int GetInt(Godot.Collections.Dictionary d, string key, int fallback)
        => d.TryGetValue(key, out var v) ? v.AsInt32() : fallback;
}

public class CargoType
{
    public string Name { get; set; } = "";
    public Color Color { get; set; } = Colors.Gray;
}
