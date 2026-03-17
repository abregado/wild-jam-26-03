using Godot;
using System.Collections.Generic;
using System.Linq;

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
    public List<ContainerNode> AllContainers { get; } = new();
    private readonly List<Carriage> _carriages = new();

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
        _carriages.Clear();

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        int numCarriages = rng.RandiRange(_config.MinCarriages, _config.MaxCarriages);

        // Total length: caboose + carriages + loco (plus gaps between each car)
        int numGaps = 1 + numCarriages + 1; // caboose→carriage(s)→loco
        float totalLength = CabooseLength + numCarriages * CarriageLength + LocoLength + numGaps * CarGap;

        // Caboose at Z=0, locomotive at Z=totalLength
        float currentZ = 0f;

        // --- Caboose ---
        var caboose = CreateBoxCar("Caboose", new Vector3(CarriageWidth, CarriageHeight, CabooseLength),
                                    new Color(0.4f, 0.2f, 0.1f),
                                    "res://assets/models/train/caboose.glb");
        caboose.Position = new Vector3(0, CarriageY, currentZ + CabooseLength / 2f);
        AddChild(caboose);
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

            int numDeployers = rng.RandiRange(1, _config.MaxDeployersPerCarriage);
            AttachDeployers(carriageInstance, numDeployers, zCenter, rng);

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
        foreach (var carriage in _carriages)
            foreach (var deployer in carriage.Deployers)
                deployer.SetPlayerCar(_playerCar);
        WireDeployerActivation();

        GD.Print($"[TrainBuilder] Built {numCarriages} carriages. LocomotiveZ={LocomotiveZ}, Containers={AllContainers.Count}");
    }

    private void WireDeployerActivation()
    {
        for (int i = 0; i < _carriages.Count; i++)
        {
            if (_carriages[i].Deployers.Count == 0) continue;

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
                }
            }
        }
    }

    private void AttachContainers(Carriage carriage, RandomNumberGenerator rng)
    {
        int slotsPerSide = _config.MaxContainersPerCarriage;
        float spacing = ContainerDepth + 0.3f;
        float startZ = -(slotsPerSide - 1) * spacing / 2f;

        var gameSession = GetNode<GameSession>("/root/GameSession");
        var trainSpeedManager = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");

        // Build containers on both sides — right (+X) then left (-X)
        var allContainers = new List<(ContainerNode node, bool isRightSide)>();
        foreach (int side in new[] { 1, -1 })
        {
            float xPos = side * ContainerXOffset;
            for (int i = 0; i < slotsPerSide; i++)
            {
                var containerInstance = (ContainerNode)_containerScene.Instantiate();
                containerInstance.Name = $"Container_{carriage.Name}_{(side > 0 ? "R" : "L")}_{i}";

                containerInstance.Position = new Vector3(xPos, 0f, startZ + i * spacing);
                carriage.AddChild(containerInstance);

                int cargoIndex = rng.RandiRange(0, _config.CargoTypes.Count - 1);
                containerInstance.SetCargoType(_config.CargoTypes[cargoIndex]);

                containerInstance.CargoDetached += gameSession.OnCargoDetached;
                containerInstance.CargoDetached += _ => trainSpeedManager.OnContainerDetached();
                containerInstance.ContainerDestroyed += gameSession.OnContainerDestroyed;
                containerInstance.ContainerDestroyed += trainSpeedManager.OnContainerDestroyed;

                allContainers.Add((containerInstance, side > 0));
                AllContainers.Add(containerInstance);
            }
        }

        // Choose which containers get clamps; the rest are locked (damageable but not detachable)
        int numClamped = Mathf.Min(
            rng.RandiRange(_config.MinContainersPerCarriage, _config.MaxContainersPerCarriage),
            allContainers.Count);

        // Fisher-Yates shuffle to pick clamped containers randomly
        var indices = Enumerable.Range(0, allContainers.Count).ToList();
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        var clampedSet = new HashSet<int>(indices.Take(numClamped));

        for (int i = 0; i < allContainers.Count; i++)
        {
            var (container, isRightSide) = allContainers[i];
            if (clampedSet.Contains(i))
                AttachClamps(container, isRightSide, rng);
            else
                container.SetLocked();
        }
    }

    private void AttachDeployers(Carriage carriage, int count, float carriageZCenter, RandomNumberGenerator rng)
    {
        float spacing = CarriageLength / (count + 1);
        float localY = CarriageHeight / 2f + DeployerHeight / 2f;

        for (int i = 0; i < count; i++)
        {
            var deployer = new DeployerNode();
            deployer.Name = $"Deployer_{carriage.Name}_{i}";
            float zOffset = -CarriageLength / 2f + spacing * (i + 1);
            deployer.Position = new Vector3(0f, localY, zOffset);
            carriage.AddChild(deployer);
            carriage.Deployers.Add(deployer);
        }
    }

    private void AttachClamps(ContainerNode container, bool isRightSide, RandomNumberGenerator rng)
    {
        int count = rng.RandiRange(_config.MinClampsPerContainer, _config.MaxClampsPerContainer);
        float spacing = ContainerDepth / (count + 1);
        // Outward face: +X for right-side containers, -X for left-side
        float clampX = isRightSide ? ContainerWidth / 2f + 0.15f : -(ContainerWidth / 2f + 0.15f);

        for (int i = 0; i < count; i++)
        {
            var clampInstance = (ClampNode)_clampScene.Instantiate();
            clampInstance.Name = $"Clamp_{i}";

            float zOffset = -ContainerDepth / 2f + spacing * (i + 1);
            clampInstance.Position = new Vector3(clampX, 0f, zOffset);
            container.AddChild(clampInstance);
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