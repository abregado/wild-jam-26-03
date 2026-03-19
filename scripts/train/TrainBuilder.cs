using Godot;
using System.Collections.Generic;

/// <summary>
/// Builds the train at runtime from config values.
/// Attach to a Node3D called "Train" in Main.tscn.
///
/// Train layout (Z axis, positive Z = forward/front of train):
///   Locomotive at highest Z.
///   Carriages extend in -Z direction.
///   Caboose at lowest Z.
///
/// Carriage spacing: 12 units. Locomotive/Caboose length: 8 units. Carriage length: 10 units.
/// Container attachment: 1-3 containers per carriage side (right side, X = +2.0).
/// Containers stacked vertically if > 1 per slot.
///
/// Exposes LocomotiveZ (world Z of locomotive front) for LevelManager.
/// Exposes AllContainers list for LevelManager range checks.
/// </summary>
public partial class TrainBuilder : Node3D
{
    private const float CarriageLength = 12f;
    private const float LocoLength = 10f;
    private const float CabooseLength = 8f;
    private const float CarGap = 0.5f;
    private const float CarriageWidth = 3f;
    private const float CarriageHeight = 2.5f;
    private const float TrackY = 7f;    // Y position of track surface
    private const float CarriageY = TrackY + CarriageHeight / 2f;

    private const float ContainerWidth = 2.0f;
    private const float ContainerHeight = 2.0f;
    private const float ContainerDepth = 3.0f;
    private const float ContainerXOffset = CarriageWidth / 2f + ContainerWidth / 2f + 0.1f;

    private PackedScene _carriageScene = null!;
    private PackedScene _containerScene = null!;
    private PackedScene _clampScene = null!;
    private GameConfig _config = null!;

    private const float DeployerHeight = 0.4f;

    public float LocomotiveZ { get; private set; }
    public float CabooseZ    { get; private set; }   // world-local Z of the caboose rear
    public List<ContainerNode> AllContainers { get; } = new();
    public List<DeployerNode>  AllDeployers  { get; } = new();
    public List<RoofTurretNode> AllRoofTurrets { get; } = new();
    public Node3D? CabooseNode { get; private set; }
    private readonly List<Carriage> _carriages = new();
    private bool _isFirstBuild = true;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _carriageScene = GD.Load<PackedScene>("res://scenes/train/Carriage.tscn");
        _containerScene = GD.Load<PackedScene>("res://scenes/train/Container.tscn");
        _clampScene = GD.Load<PackedScene>("res://scenes/train/Clamp.tscn");

        BuildTrain();
    }

    private PlayerCar? _playerCar;

    private void BuildTrain()
    {
        // Clear previous train (for Play Again)
        foreach (Node child in GetChildren())
            child.QueueFree();
        AllContainers.Clear();
        AllDeployers.Clear();
        AllRoofTurrets.Clear();
        _carriages.Clear();

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        int numCarriages = rng.RandiRange(_config.MinCarriages, _config.MaxCarriages);

        // Total length: caboose + carriages + loco (plus gaps between each car)
        int numGaps = 1 + numCarriages + 1; // caboose→carriage(s)→loco
        float totalLength = CabooseLength + numCarriages * CarriageLength + LocoLength + numGaps * CarGap;

        // Caboose at Z=0, locomotive at Z=totalLength
        float currentZ = 0f;
        CabooseZ = currentZ;   // rear of caboose in Train-local space

        // --- Caboose ---
        var caboose = CreateBoxCar("Caboose", new Vector3(CarriageWidth, CarriageHeight, CabooseLength),
                                    new Color(0.4f, 0.2f, 0.1f),
                                    "res://assets/models/train/caboose.glb");
        caboose.Position = new Vector3(0, CarriageY, currentZ + CabooseLength / 2f);
        AddChild(caboose);
        CabooseNode = caboose;
        currentZ += CabooseLength + CarGap;

        // --- Carriages ---
        for (int i = 0; i < numCarriages; i++)
        {
            var carriageInstance = (Carriage)_carriageScene.Instantiate();
            carriageInstance.Name = $"Carriage{i}";
            float zCenter = currentZ + CarriageLength / 2f;
            carriageInstance.Position = new Vector3(0, CarriageY, zCenter);
            AddChild(carriageInstance);
            _carriages.Add(carriageInstance);

            AttachContainers(carriageInstance, rng);

            int numSlots = rng.RandiRange(1, _config.MaxDeployersPerCarriage);
            AttachRoofSlots(carriageInstance, numSlots, rng);

            currentZ += CarriageLength + CarGap;
        }

        // --- Locomotive ---
        var loco = CreateBoxCar("Locomotive", new Vector3(CarriageWidth, CarriageHeight + 0.5f, LocoLength),
                                 new Color(0.6f, 0.1f, 0.1f),
                                 "res://assets/models/train/locomotive.glb");
        loco.Position = new Vector3(0, CarriageY + 0.25f, currentZ + LocoLength / 2f);
        AddChild(loco);
        LocomotiveZ = currentZ + LocoLength;

        // Deployers + activation signal wiring (done after all carriages are built)
        _playerCar ??= GetTree().Root.FindChild("PlayerCar", true, false) as PlayerCar;

        // First build: guarantee at least one deployer and one roof turret exist
        if (_isFirstBuild)
        {
            bool hasDeployer = false;
            bool hasTurret   = false;
            foreach (var c in _carriages)
            {
                if (c.Deployers.Count > 0)   hasDeployer = true;
                if (c.RoofTurrets.Count > 0) hasTurret   = true;
            }

            float localY = CarriageHeight / 2f + DeployerHeight / 2f;

            if (!hasDeployer && _carriages.Count > 0)
            {
                var target = _carriages[_carriages.Count / 2];
                var d = new DeployerNode();
                d.Name     = $"Deployer_{target.Name}_forced";
                d.Position = new Vector3(0f, localY, 0f);
                target.AddChild(d);
                target.Deployers.Add(d);
            }

            if (!hasTurret && _carriages.Count > 0)
            {
                int idx    = _carriages.Count > 1 ? (_carriages.Count / 2 + 1) % _carriages.Count : 0;
                var target = _carriages[idx];
                var t = new RoofTurretNode();
                t.Name     = $"RoofTurret_{target.Name}_forced";
                t.Position = new Vector3(0f, localY, CarriageLength * 0.25f);
                target.AddChild(t);
                target.RoofTurrets.Add(t);
            }

            _isFirstBuild = false;
        }

        foreach (var carriage in _carriages)
        {
            foreach (var deployer in carriage.Deployers)
                deployer.SetPlayerCar(_playerCar);
            foreach (var turret in carriage.RoofTurrets)
                turret.SetPlayerCar(_playerCar);

            AllDeployers.AddRange(carriage.Deployers);
            AllRoofTurrets.AddRange(carriage.RoofTurrets);
        }
        WireDeployerActivation();

        GD.Print($"[TrainBuilder] Built {numCarriages} carriages. LocomotiveZ={LocomotiveZ}, Containers={AllContainers.Count}");
    }

    private void WireDeployerActivation()
    {
        for (int i = 0; i < _carriages.Count; i++)
        {
            bool hasRoofEnemies = _carriages[i].Deployers.Count > 0
                                || _carriages[i].RoofTurrets.Count > 0;
            if (!hasRoofEnemies) continue;

            // Connect containers/clamps from this carriage and direct neighbors.
            // NOTE: iterate GetChildren() directly — Carriage.Containers is populated in
            // _Ready() which fires before containers are attached, so that list is empty.
            for (int j = Mathf.Max(0, i - 1); j <= Mathf.Min(_carriages.Count - 1, i + 1); j++)
            {
                foreach (Node carriageChild in _carriages[j].GetChildren())
                {
                    if (carriageChild is not ContainerNode container) continue;

                    foreach (var deployer in _carriages[i].Deployers)
                    {
                        container.DamageTaken += deployer.Activate;
                        foreach (Node containerChild in container.GetChildren())
                        {
                            if (containerChild is ClampNode clamp)
                                clamp.DamageTaken += deployer.Activate;
                        }
                    }

                    foreach (var turret in _carriages[i].RoofTurrets)
                    {
                        container.DamageTaken += turret.Activate;
                        foreach (Node containerChild in container.GetChildren())
                        {
                            if (containerChild is ClampNode clamp)
                                clamp.DamageTaken += turret.Activate;
                        }
                    }
                }
            }
        }
    }

    private enum ClampSetup { Single, Double, Triple, Four }

    private ClampSetup PickClampSetup(RandomNumberGenerator rng)
    {
        float total = _config.ClampSetupWeightSingle + _config.ClampSetupWeightDouble
                    + _config.ClampSetupWeightTriple + _config.ClampSetupWeightFour;
        float roll = rng.Randf() * total;
        if (roll < _config.ClampSetupWeightSingle) return ClampSetup.Single;
        roll -= _config.ClampSetupWeightSingle;
        if (roll < _config.ClampSetupWeightDouble) return ClampSetup.Double;
        roll -= _config.ClampSetupWeightDouble;
        if (roll < _config.ClampSetupWeightTriple) return ClampSetup.Triple;
        return ClampSetup.Four;
    }

    private void AttachContainers(Carriage carriage, RandomNumberGenerator rng)
    {
        int slotsPerSide = _config.MaxContainersPerCarriage;
        float spacing = ContainerDepth + 0.3f;
        float startZ = -(slotsPerSide - 1) * spacing / 2f;

        var gameSession = GetNode<GameSession>("/root/GameSession");
        var trainSpeedManager = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");

        // Determine clamp setup for all containers on this carriage
        var setup = PickClampSetup(rng);

        // Build containers on both sides — right (+X) then left (-X)
        foreach (int side in new[] { 1, -1 })
        {
            bool isRightSide = side > 0;
            float xPos = side * ContainerXOffset;
            for (int i = 0; i < slotsPerSide; i++)
            {
                var containerInstance = (ContainerNode)_containerScene.Instantiate();
                containerInstance.Name = $"Container_{carriage.Name}_{(isRightSide ? "R" : "L")}_{i}";

                containerInstance.Position = new Vector3(xPos, 0f, startZ + i * spacing);
                carriage.AddChild(containerInstance);

                int cargoIndex = rng.RandiRange(0, _config.CargoTypes.Count - 1);
                containerInstance.SetCargoType(_config.CargoTypes[cargoIndex]);

                containerInstance.CargoDetached += gameSession.OnCargoDetached;
                containerInstance.CargoDetached += (_, _) => trainSpeedManager.OnContainerDetached();
                containerInstance.ContainerDestroyed += gameSession.OnContainerDestroyed;
                containerInstance.ContainerDestroyed += trainSpeedManager.OnContainerDestroyed;

                AllContainers.Add(containerInstance);
                AttachClampsForSetup(containerInstance, isRightSide, setup, rng);
            }
        }
    }

    /// <summary>
    /// Fills up to <paramref name="count"/> roof slots with either a DeployerNode or a
    /// RoofTurretNode chosen at random (50/50). Slots are evenly spaced along the carriage.
    /// </summary>
    private void AttachRoofSlots(Carriage carriage, int count, RandomNumberGenerator rng)
    {
        float spacing = CarriageLength / (count + 1);
        float localY = CarriageHeight / 2f + DeployerHeight / 2f;

        for (int i = 0; i < count; i++)
        {
            float zOffset = -CarriageLength / 2f + spacing * (i + 1);

            if (rng.Randf() < 0.5f)
            {
                var deployer = new DeployerNode();
                deployer.Name = $"Deployer_{carriage.Name}_{i}";
                deployer.Position = new Vector3(0f, localY, zOffset);
                carriage.AddChild(deployer);
                carriage.Deployers.Add(deployer);
            }
            else
            {
                var turret = new RoofTurretNode();
                turret.Name = $"RoofTurret_{carriage.Name}_{i}";
                turret.Position = new Vector3(0f, localY, zOffset);
                carriage.AddChild(turret);
                carriage.RoofTurrets.Add(turret);
            }
        }
    }

    private void AttachClampsForSetup(ContainerNode container, bool isRightSide, ClampSetup setup, RandomNumberGenerator rng)
    {
        float clampX     = isRightSide ? ContainerWidth / 2f + 0.15f : -(ContainerWidth / 2f + 0.15f);
        float halfH      = ContainerHeight / 2f;
        float halfD      = ContainerDepth  / 2f;
        const float faceOff = 0.15f;

        float hp;
        Vector3[] positions;

        switch (setup)
        {
            case ClampSetup.Single:
                hp = _config.SingleClampHp;
                positions = new[] { new Vector3(clampX, 0f, 0f) };
                break;

            case ClampSetup.Double:
                hp = _config.DoubleClampHp;
                // 3 face-centre candidates; pick 2 at random via Fisher-Yates
                var candidates = new[]
                {
                    new Vector3(clampX, 0f,                0f),  // outward face
                    new Vector3(0f,     halfH + faceOff,   0f),  // top face
                    new Vector3(0f,   -(halfH + faceOff),  0f),  // bottom face
                };
                for (int i = 2; i > 0; i--)
                {
                    int j = rng.RandiRange(0, i);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                positions = new[] { candidates[0], candidates[1] };
                break;

            case ClampSetup.Triple:
                hp = _config.TripleClampHp;
                float zStep = ContainerDepth / 3f;
                positions = new[]
                {
                    new Vector3(0f, halfH + faceOff, -zStep),
                    new Vector3(0f, halfH + faceOff,  0f),
                    new Vector3(0f, halfH + faceOff,  zStep),
                };
                break;

            default: // Four
                hp = _config.FourClampHp;
                positions = new[]
                {
                    new Vector3(clampX,  halfH * 0.7f,  halfD * 0.7f),
                    new Vector3(clampX,  halfH * 0.7f, -halfD * 0.7f),
                    new Vector3(clampX, -halfH * 0.7f,  halfD * 0.7f),
                    new Vector3(clampX, -halfH * 0.7f, -halfD * 0.7f),
                };
                break;
        }

        foreach (var pos in positions)
        {
            var clampInstance = (ClampNode)_clampScene.Instantiate();
            clampInstance.Name = $"Clamp_{pos.X:F1}_{pos.Y:F1}_{pos.Z:F1}";
            clampInstance.Position = pos;
            container.AddChild(clampInstance);
            clampInstance.SetHitpoints(hp);
            container.RegisterClamp(clampInstance);
        }
    }

    private static Node3D CreateBoxCar(string name, Vector3 size, Color color, string? glbPath = null)
    {
        var node = new Node3D { Name = name };

        var meshSlot = new MeshInstance3D { Name = "MeshSlot" };
        var loadedMesh = glbPath != null ? TryLoadGlbMesh(glbPath) : null;
        if (loadedMesh != null)
        {
            meshSlot.Mesh = loadedMesh;
        }
        else
        {
            var box = new BoxMesh { Size = size };
            box.Material = new StandardMaterial3D { AlbedoColor = color };
            meshSlot.Mesh = box;
        }
        node.AddChild(meshSlot);

        // Layer 1 (World/Train) — detectable by aim raycasts but not by bullets
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Name = "TrainBody" };
        var col = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
        body.AddChild(col);
        node.AddChild(body);

        return node;
    }

    /// <summary>
    /// Loads the 'Body' mesh from a GLB scene file.
    /// Returns null if the file is not found or has no Body node — caller falls back to procedural.
    /// </summary>
    private static Mesh? TryLoadGlbMesh(string glbPath)
    {
        var scene = GD.Load<PackedScene>(glbPath);
        if (scene == null) return null;

        var root = scene.Instantiate<Node3D>();
        var bodyNode = root.FindChild("Body") as MeshInstance3D;
        var mesh = bodyNode?.Mesh;
        root.QueueFree();
        return mesh;
    }

    /// <summary>Rebuild train for a new game (called by LevelManager on Play Again).</summary>
    public void Rebuild()
    {
        BuildTrain();
    }
}