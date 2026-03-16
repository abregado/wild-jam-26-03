using Godot;

/// <summary>
/// Manages the scrolling ground, track, and pillar pool.
/// Polls TrainSpeedManager.CurrentTrainSpeed each frame.
///
/// Ground shader parameter: "scroll_speed" (float, units per second)
/// Track shader parameter: "scroll_speed" (float, units per second)
///
/// Scroll direction convention: positive scroll_speed = ground moves in -Z direction
/// (simulates train moving in +Z / forward direction).
/// </summary>
public partial class TrackEnvironment : Node3D
{
    [Export] public NodePath GroundMeshPath { get; set; } = "GroundPlane";
    [Export] public NodePath TrackMeshPath { get; set; } = "TrackMesh";

    private MeshInstance3D _groundMesh = null!;
    private MeshInstance3D _trackMesh = null!;
    private ShaderMaterial? _groundMaterial;
    private ShaderMaterial? _trackMaterial;
    private PillarPool _pillarPool = null!;

    public override void _Ready()
    {
        _groundMesh = GetNode<MeshInstance3D>(GroundMeshPath);
        _trackMesh = GetNode<MeshInstance3D>(TrackMeshPath);
        _groundMaterial = _groundMesh.GetActiveMaterial(0) as ShaderMaterial;
        _trackMaterial = _trackMesh.GetActiveMaterial(0) as ShaderMaterial;
        _pillarPool = GetNode<PillarPool>("PillarPool");
    }

    public void SetScrollSpeed(float unitsPerSecond)
    {
        _groundMaterial?.SetShaderParameter("scroll_speed", unitsPerSecond);
        _trackMaterial?.SetShaderParameter("scroll_speed", unitsPerSecond);
        _pillarPool?.SetMoveSpeed(unitsPerSecond);
    }

    public override void _Process(double delta)
    {
        var tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        SetScrollSpeed(tsm.CurrentTrainSpeed);
    }
}
