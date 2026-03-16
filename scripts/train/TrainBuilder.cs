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
    private const float TrackY = 3f;    // Y position of track surface
    private const float CarriageY = TrackY + CarriageHeight / 2f;

    private const float ContainerWidth = 2.0f;
    private const float ContainerHeight = 2.0f;
    private const float ContainerDepth = 3.0f;
    private const float ContainerXOffset = CarriageWidth / 2f + ContainerWidth / 2f + 0.1f;

    private PackedScene _carriageScene = null!;
    private PackedScene _containerScene = null!;
    private PackedScene _clampScene = null!;
    private GameConfig _config = null!;

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
                                    new Color(0.4f, 0.2f, 0.1f));
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

            int numContainers = rng.RandiRange(_config.MinContainersPerCarriage, _config.MaxContainersPerCarriage);
            AttachContainers(carriageInstance, numContainers, zCenter, rng);

            currentZ += CarriageLength + CarGap;
        }

        // --- Locomotive ---
        var loco = CreateBoxCar("Locomotive", new Vector3(CarriageWidth, CarriageHeight + 0.5f, LocoLength),
                                 new Color(0.6f, 0.1f, 0.1f));
        loco.Position = new Vector3(0, CarriageY + 0.25f, currentZ + LocoLength / 2f);
        AddChild(loco);
        LocomotiveZ = currentZ + LocoLength;

        GD.Print($"[TrainBuilder] Built {numCarriages} carriages. LocomotiveZ={LocomotiveZ}, Containers={AllContainers.Count}");
    }

    private void AttachContainers(Carriage carriage, int count, float carriageZCenter, RandomNumberGenerator rng)
    {
        float spacing = ContainerDepth + 0.3f;
        float startZ = carriageZCenter - (count - 1) * spacing / 2f;

        var gameSession = GetNode<GameSession>("/root/GameSession");
        var trainSpeedManager = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");

        for (int i = 0; i < count; i++)
        {
            var containerInstance = (ContainerNode)_containerScene.Instantiate();
            containerInstance.Name = $"Container_{carriage.Name}_{i}";

            float zPos = startZ + i * spacing;
            containerInstance.Position = new Vector3(ContainerXOffset, 0f, zPos - carriageZCenter);
            carriage.AddChild(containerInstance);

            // Assign random cargo type
            int cargoIndex = rng.RandiRange(0, _config.CargoTypes.Count - 1);
            var cargoType = _config.CargoTypes[cargoIndex];
            containerInstance.SetCargoType(cargoType);

            // Connect signals
            containerInstance.CargoDetached += gameSession.OnCargoDetached;
            containerInstance.CargoDetached += _ => trainSpeedManager.OnContainerDetached();
            containerInstance.ContainerDestroyed += gameSession.OnContainerDestroyed;
            containerInstance.ContainerDestroyed += trainSpeedManager.OnContainerDestroyed;

            AttachClamps(containerInstance, rng);
            AllContainers.Add(containerInstance);
        }
    }

    private void AttachClamps(ContainerNode container, RandomNumberGenerator rng)
    {
        int count = rng.RandiRange(_config.MinClampsPerContainer, _config.MaxClampsPerContainer);
        float spacing = ContainerDepth / (count + 1);

        for (int i = 0; i < count; i++)
        {
            var clampInstance = (ClampNode)_clampScene.Instantiate();
            clampInstance.Name = $"Clamp_{i}";

            // Distribute clamps along container surface (outward face = +X side)
            float zOffset = -ContainerDepth / 2f + spacing * (i + 1);
            clampInstance.Position = new Vector3(ContainerWidth / 2f + 0.15f, 0f, zOffset);
            container.AddChild(clampInstance);
            container.RegisterClamp(clampInstance);
        }
    }

    private static Node3D CreateBoxCar(string name, Vector3 size, Color color)
    {
        var node = new Node3D { Name = name };

        var mesh = new MeshInstance3D { Name = "MeshSlot" };
        var box = new BoxMesh { Size = size };
        var mat = new StandardMaterial3D { AlbedoColor = color };
        box.Material = mat;
        mesh.Mesh = box;
        node.AddChild(mesh);

        // Layer 1 (World/Train) — detectable by aim raycasts but not by bullets
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Name = "TrainBody" };
        var col = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
        body.AddChild(col);
        node.AddChild(body);

        return node;
    }

    /// <summary>Rebuild train for a new game (called by LevelManager on Play Again).</summary>
    public void Rebuild()
    {
        BuildTrain();
    }
}