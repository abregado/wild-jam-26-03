using Godot;

/// <summary>
/// Manages the scrolling ground, track, pillar pool, and all decorative pools
/// (clouds, rock pillars, rubble).
/// Polls TrainSpeedManager.CurrentTrainSpeed each frame.
///
/// Ground shader parameter: "scroll_speed" (float, units per second)
/// Track shader parameter:  "scroll_speed" (float, units per second)
///
/// Scroll direction convention: positive scroll_speed = ground moves in -Z direction
/// (simulates train moving in +Z / forward direction).
///
/// Ground plane is resized on the first frame that the Train node is available, so
/// its extents match:
///   Z : −2×DespawnBehindDistance  …  LocomotiveZ + 2×SpawnAheadDistance  (×1.4 padding)
///   X : ±2×RockPillarDistance                                              (×1.4 padding)
/// </summary>
public partial class TrackEnvironment : Node3D
{
    [Export] public NodePath GroundMeshPath { get; set; } = "GroundPlane";
    [Export] public NodePath TrackMeshPath  { get; set; } = "TrackMesh";

    private MeshInstance3D _groundMesh = null!;
    private MeshInstance3D _trackMesh  = null!;
    private ShaderMaterial? _groundMaterial;
    private ShaderMaterial? _trackMaterial;

    private PillarPool     _pillarPool     = null!;
    private CloudPool      _cloudPool      = null!;
    private RockPillarPool _rockPillarPool = null!;
    private RubblePool     _rubblePool     = null!;

    private StaticBody3D _groundBody = null!;
    private bool _groundResized = false;

    public override void _Ready()
    {
        _groundMesh  = GetNode<MeshInstance3D>(GroundMeshPath);
        _trackMesh   = GetNode<MeshInstance3D>(TrackMeshPath);
        _groundMaterial = _groundMesh.GetActiveMaterial(0) as ShaderMaterial;
        _trackMaterial  = _trackMesh.GetActiveMaterial(0) as ShaderMaterial;

        _pillarPool     = GetNode<PillarPool>("PillarPool");
        _cloudPool      = GetNode<CloudPool>("CloudPool");
        _rockPillarPool = GetNode<RockPillarPool>("RockPillarPool");
        _rubblePool     = GetNode<RubblePool>("RubblePool");
        _groundBody     = GetNode<StaticBody3D>("GroundBody");

        // Ground colour matches the canyon obstacle palette (average of PaletteColors)
        _groundMaterial?.SetShaderParameter("base_color",   new Color(0.58f, 0.41f, 0.21f));
        _groundMaterial?.SetShaderParameter("detail_color", new Color(0.44f, 0.30f, 0.15f));
    }

    // Called once the Train node is available so we know LocomotiveZ.
    private void ResizeGround()
    {
        var config    = GetNode<GameConfig>("/root/GameConfig");
        var trainNode = GetTree().Root.FindChild("Train", true, false);
        float locoZ   = (trainNode as TrainBuilder)?.LocomotiveZ ?? 100f;

        float totalLen  = locoZ + 2f * config.SpawnAheadDistance + 2f * config.DespawnBehindDistance;
        float totalWide = 4f * config.RockPillarDistance;

        // Centre of the Z range we need to cover
        float centerZ = (locoZ + 2f * config.SpawnAheadDistance - 2f * config.DespawnBehindDistance) / 2f;

        float padLen  = totalLen  * 1.4f;
        float padWide = totalWide * 1.4f;

        // Resize the visual plane
        if (_groundMesh.Mesh is PlaneMesh pm)
            pm.Size = new Vector2(padWide, padLen);
        _groundMesh.Position = new Vector3(0f, 0f, centerZ);

        // Resize and reposition the ground collision body
        _groundBody.Position = new Vector3(0f, -0.25f, centerZ);
        if (_groundBody.GetNode<CollisionShape3D>("GroundCollision").Shape is BoxShape3D bs)
            bs.Size = new Vector3(padWide, 0.5f, padLen);

        GD.Print($"[TrackEnvironment] Ground resized: {padWide:F0} × {padLen:F0}, centreZ={centerZ:F0}");
        _groundResized = true;
    }

    public void SetScrollSpeed(float unitsPerSecond)
    {
        _groundMaterial?.SetShaderParameter("scroll_speed", unitsPerSecond);
        _trackMaterial?.SetShaderParameter("scroll_speed", unitsPerSecond);
        _pillarPool?.SetMoveSpeed(unitsPerSecond);
        _rockPillarPool?.SetMoveSpeed(unitsPerSecond);
        _rubblePool?.SetMoveSpeed(unitsPerSecond);
        _cloudPool?.SetTrainSpeed(unitsPerSecond);
    }

    public override void _Process(double delta)
    {
        var tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        SetScrollSpeed(tsm.CurrentTrainSpeed);

        if (!_groundResized && GetTree().Root.FindChild("Train", true, false) != null)
            ResizeGround();
    }
}
