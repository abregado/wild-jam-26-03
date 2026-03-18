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
    public float BulletSize { get; private set; } = 0.55f;
    public float TrailThickness { get; private set; } = 0.18f;
    public float TurretTrackingSpeed { get; private set; } = 6f;
    public int BurstCount { get; private set; } = 3;
    public float BurstDelay { get; private set; } = 0.08f;
    public float TurretMaxPitchDown { get; private set; } = 20f;
    public bool AutoFire { get; private set; } = true;

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
    public float SideChangeTime { get; private set; } = 1.5f;
    public float CarDriveHeight { get; private set; } = 9.0f;
    public int NumberPreScannedContainers { get; private set; } = 0;
    public float CliffDetectionDistance { get; private set; } = 40f;

    // Speed
    public float BaseTrainSpeed { get; private set; } = 15f;
    public float SpeedIncreasePerContainer { get; private set; } = 2f;
    public float TurretRange { get; private set; } = 50f;

    // Environment
    public float PillarSpacing { get; private set; } = 20f;
    public float SpawnAheadDistance { get; private set; } = 40f;
    public float DespawnBehindDistance { get; private set; } = 20f;

    // Enemies
    public int MaxDronesPerDeployer { get; private set; } = 3;
    public int MaxDeployersPerCarriage { get; private set; } = 2;
    public float DeployerCooldown { get; private set; } = 8f;
    public float DroneMoveSpeed { get; private set; } = 10f;
    public float DroneCombatSpeed { get; private set; } = 5f;
    public float DroneFireRate { get; private set; } = 1.2f;
    public float CarSpeedDamagePerHit { get; private set; } = 0.3f;
    public float DroneHeightMin { get; private set; } = 2f;
    public float DroneHeightMax { get; private set; } = 5f;
    public float DroneHitpoints { get; private set; } = 25f;
    public float DroneBulletSpeed { get; private set; } = 35f;
    public float DroneBulletSize { get; private set; } = 0.12f;
    public float DroneHitChance { get; private set; } = 0.6f;
    public float ShieldBlockAngle { get; private set; } = 50f;
    public float DroneRepositionChance { get; private set; } = 0.4f;
    public float DroneChaseDistance { get; private set; } = 20f;
    public float DroneMaxDeployerDistance { get; private set; } = 50f;

    // Obstacles
    public float ObstacleSectionMinDuration { get; private set; } = 12f;
    public float ObstacleSectionMaxDuration { get; private set; } = 25f;
    public float ObstacleWarningTime { get; private set; } = 4f;
    public int ObstacleCubePoolSize { get; private set; } = 12;
    public float ObstacleCubeSpacing { get; private set; } = 6f;

    // Cargo Types
    public List<CargoType> CargoTypes { get; private set; } = new();

    // Upgrades
    public List<UpgradeDefinition> Upgrades { get; private set; } = new();

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
            BulletSize = GetFloat(t, "bullet_size", BulletSize);
            TrailThickness = GetFloat(t, "trail_thickness", TrailThickness);
            TurretTrackingSpeed = GetFloat(t, "turret_tracking_speed", TurretTrackingSpeed);
            BurstCount = GetInt(t, "burst_count", BurstCount);
            BurstDelay = GetFloat(t, "burst_delay", BurstDelay);
            TurretMaxPitchDown = GetFloat(t, "turret_max_pitch_down", TurretMaxPitchDown);
            AutoFire = GetBool(t, "auto_fire", AutoFire);
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
            MinRelativeVelocity         = GetFloat(p, "min_relative_velocity",          MinRelativeVelocity);
            MaxRelativeVelocity         = GetFloat(p, "max_relative_velocity",          MaxRelativeVelocity);
            CarAcceleration             = GetFloat(p, "car_acceleration",               CarAcceleration);
            CarDeceleration             = GetFloat(p, "car_deceleration",               CarDeceleration);
            SideChangeTime              = GetFloat(p, "side_change_time",               SideChangeTime);
            CarDriveHeight              = GetFloat(p, "car_drive_height",               CarDriveHeight);
            NumberPreScannedContainers  = GetInt  (p, "number_pre_scanned_containers",  NumberPreScannedContainers);
            CliffDetectionDistance      = GetFloat(p, "cliff_detection_distance",       CliffDetectionDistance);
        }

        if (data.TryGetValue("speed", out var speedVar))
        {
            var s = speedVar.AsGodotDictionary();
            BaseTrainSpeed = GetFloat(s, "base_train_speed", BaseTrainSpeed);
            SpeedIncreasePerContainer = GetFloat(s, "speed_increase_per_container", SpeedIncreasePerContainer);
            TurretRange = GetFloat(s, "turret_range", TurretRange);
        }

        if (data.TryGetValue("environment", out var envVar))
        {
            var e = envVar.AsGodotDictionary();
            PillarSpacing          = GetFloat(e, "pillar_spacing",           PillarSpacing);
            SpawnAheadDistance     = GetFloat(e, "spawn_ahead_distance",     SpawnAheadDistance);
            DespawnBehindDistance  = GetFloat(e, "despawn_behind_distance",  DespawnBehindDistance);
        }

        if (data.TryGetValue("enemies", out var enemiesVar))
        {
            var en = enemiesVar.AsGodotDictionary();
            MaxDronesPerDeployer = GetInt(en, "max_drones_per_deployer", MaxDronesPerDeployer);
            MaxDeployersPerCarriage = GetInt(en, "max_deployers_per_carriage", MaxDeployersPerCarriage);
            DeployerCooldown = GetFloat(en, "deployer_cooldown", DeployerCooldown);
            DroneMoveSpeed = GetFloat(en, "drone_move_speed", DroneMoveSpeed);
            DroneCombatSpeed = GetFloat(en, "drone_combat_speed", DroneCombatSpeed);
            DroneFireRate = GetFloat(en, "drone_fire_rate", DroneFireRate);
            CarSpeedDamagePerHit = GetFloat(en, "car_speed_damage_per_hit", CarSpeedDamagePerHit);
            DroneHeightMin = GetFloat(en, "drone_height_min", DroneHeightMin);
            DroneHeightMax = GetFloat(en, "drone_height_max", DroneHeightMax);
            DroneHitpoints = GetFloat(en, "drone_hitpoints", DroneHitpoints);
            DroneBulletSpeed = GetFloat(en, "drone_bullet_speed", DroneBulletSpeed);
            DroneBulletSize = GetFloat(en, "drone_bullet_size", DroneBulletSize);
            DroneHitChance = GetFloat(en, "drone_hit_chance", DroneHitChance);
            ShieldBlockAngle = GetFloat(en, "shield_block_angle", ShieldBlockAngle);
            DroneRepositionChance = GetFloat(en, "drone_reposition_chance", DroneRepositionChance);
            DroneChaseDistance = GetFloat(en, "drone_chase_distance", DroneChaseDistance);
            DroneMaxDeployerDistance = GetFloat(en, "drone_max_deployer_distance", DroneMaxDeployerDistance);
        }

        if (data.TryGetValue("obstacles", out var obstaclesVar))
        {
            var o = obstaclesVar.AsGodotDictionary();
            ObstacleSectionMinDuration = GetFloat(o, "section_min_duration", ObstacleSectionMinDuration);
            ObstacleSectionMaxDuration = GetFloat(o, "section_max_duration", ObstacleSectionMaxDuration);
            ObstacleWarningTime        = GetFloat(o, "warning_time",         ObstacleWarningTime);
            ObstacleCubePoolSize       = GetInt  (o, "cube_pool_size",       ObstacleCubePoolSize);
            ObstacleCubeSpacing        = GetFloat(o, "cube_spacing",         ObstacleCubeSpacing);
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

        if (data.TryGetValue("upgrades", out var upgradesVar))
        {
            Upgrades.Clear();
            var arr = upgradesVar.AsGodotArray();
            foreach (var item in arr)
            {
                var d = item.AsGodotDictionary();
                var upgrade = new UpgradeDefinition
                {
                    Id          = d.TryGetValue("id",          out var idV)   ? idV.AsString()   : "",
                    Name        = d.TryGetValue("name",        out var nameV) ? nameV.AsString() : "",
                    Description = d.TryGetValue("description", out var descV) ? descV.AsString() : "",
                };
                if (d.TryGetValue("modifiers", out var modsV))
                {
                    foreach (var modItem in modsV.AsGodotArray())
                    {
                        var md = modItem.AsGodotDictionary();
                        upgrade.Modifiers.Add(new StatModifier
                        {
                            Stat       = md.TryGetValue("stat",       out var sv) ? sv.AsString()         : "",
                            Flat       = md.TryGetValue("flat",       out var fv) ? (float)fv.AsDouble()  : 0f,
                            Multiplier = md.TryGetValue("multiplier", out var mv) ? (float)mv.AsDouble()  : 1f,
                        });
                    }
                }
                if (d.TryGetValue("cost", out var costV))
                {
                    foreach (var kv in costV.AsGodotDictionary())
                        upgrade.Cost[kv.Key.AsString()] = kv.Value.AsInt32();
                }
                Upgrades.Add(upgrade);
            }
        }
    }

    /// <summary>
    /// Applies an upgrade's modifiers to the live config values.
    /// All flat deltas are added first, then all multipliers are applied.
    /// </summary>
    public void ApplyUpgrade(UpgradeDefinition u)
    {
        foreach (var m in u.Modifiers)
            if (m.Flat != 0f) ApplyFlat(m.Stat, m.Flat);

        foreach (var m in u.Modifiers)
            if (m.Multiplier != 1f) ApplyMult(m.Stat, m.Multiplier);
    }

    private void ApplyFlat(string stat, float v)
    {
        switch (stat)
        {
            case "turret_tracking_speed":    TurretTrackingSpeed   += v; break;
            case "turret_damage":            TurretDamage          += v; break;
            case "burst_count":              BurstCount             = Mathf.Max(1, BurstCount + (int)v); break;
            case "burst_delay":              BurstDelay            += v; break;
            case "rate_of_fire":             RateOfFire            += v; break;
            case "bullet_speed":             BulletSpeed           += v; break;
            case "beacon_reload_speed":      BeaconReloadSpeed     += v; break;
            case "max_relative_velocity":             MaxRelativeVelocity          += v; break;
            case "number_pre_scanned_containers":     NumberPreScannedContainers    = Mathf.Max(0, NumberPreScannedContainers + (int)v); break;
            case "blast_radius":             BlastRadius           += v; break;
            case "shield_block_angle":       ShieldBlockAngle      += v; break;
            case "car_speed_damage_per_hit": CarSpeedDamagePerHit  += v; break;
        }
    }

    private void ApplyMult(string stat, float m)
    {
        switch (stat)
        {
            case "turret_tracking_speed":    TurretTrackingSpeed   *= m; break;
            case "turret_damage":            TurretDamage          *= m; break;
            case "burst_count":              BurstCount             = Mathf.Max(1, (int)(BurstCount * m)); break;
            case "burst_delay":              BurstDelay            *= m; break;
            case "rate_of_fire":             RateOfFire            *= m; break;
            case "bullet_speed":             BulletSpeed           *= m; break;
            case "beacon_reload_speed":      BeaconReloadSpeed     *= m; break;
            case "max_relative_velocity":    MaxRelativeVelocity   *= m; break;
            case "car_drive_height":         CarDriveHeight        *= m; break;
            case "blast_radius":             BlastRadius           *= m; break;
            case "shield_block_angle":       ShieldBlockAngle      *= m; break;
            case "car_speed_damage_per_hit": CarSpeedDamagePerHit  *= m; break;
        }
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

    private static bool GetBool(Godot.Collections.Dictionary d, string key, bool fallback)
        => d.TryGetValue(key, out var v) ? v.AsBool() : fallback;
}

public class CargoType
{
    public string Name { get; set; } = "";
    public Color Color { get; set; } = Colors.Gray;
}

public class StatModifier
{
    public string Stat       { get; set; } = "";
    public float  Flat       { get; set; } = 0f;   // additive delta (applied first)
    public float  Multiplier { get; set; } = 1f;   // multiplicative scale (applied after all flats)
}

public class UpgradeDefinition
{
    public string Id          { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public List<StatModifier>      Modifiers { get; set; } = new();
    public Dictionary<string, int> Cost      { get; set; } = new();
}
