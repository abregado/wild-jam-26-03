using Godot;
using System.Collections.Generic;

/// <summary>
/// Script for a train carriage node.
/// Holds the list of container children for reference.
///
/// SetSlotCount(n) swaps the MeshSlot mesh to the model appropriate for n containers per side:
///   1 → carriage_1.glb  (or procedural 4-unit-long box)
///   2 → carriage_2.glb  (or procedural 8-unit-long box)
///   3 → carriage_3.glb  (or procedural 12-unit-long box)
/// </summary>
public partial class Carriage : Node3D
{
    public List<ContainerNode> Containers { get; } = new();
    public List<DeployerNode> Deployers { get; } = new();
    public List<RoofTurretNode> RoofTurrets { get; } = new();

    private static readonly Color CarriageColor = new(0.1f, 0.3f, 0.85f);

    // Carriage body dimensions
    private const float Width  = 3.0f;
    private const float Height = 2.5f;

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is ContainerNode c)
                Containers.Add(c);
        }
    }

    /// <summary>
    /// Called by TrainBuilder after instantiation to select the correct carriage mesh.
    /// </summary>
    public void SetSlotCount(int slots)
    {
        var meshSlot = GetNodeOrNull<MeshInstance3D>("MeshSlot");
        if (meshSlot == null) return;

        // Try to load the matching GLB
        string glbPath = $"res://assets/models/train/carriage_{Mathf.Clamp(slots, 1, 3)}.glb";
        var scene = GD.Load<PackedScene>(glbPath);
        if (scene != null)
        {
            var root = scene.Instantiate<Node3D>();
            var body = root.FindChild("Body") as MeshInstance3D;
            if (body?.Mesh != null)
            {
                meshSlot.Mesh = body.Mesh;
                meshSlot.MaterialOverride = new StandardMaterial3D { AlbedoColor = CarriageColor };
                root.QueueFree();
                UpdateCollision(slots);
                return;
            }
            root.QueueFree();
        }

        // Procedural fallback: scale Z length proportionally to slot count
        float zLen = 4.0f * slots;
        meshSlot.Mesh = new BoxMesh { Size = new Vector3(Width, Height, zLen) };
        meshSlot.MaterialOverride = new StandardMaterial3D { AlbedoColor = CarriageColor };
        UpdateCollision(slots);
    }

    private void UpdateCollision(int slots)
    {
        float zLen = 4.0f * slots;
        var body = GetNodeOrNull<StaticBody3D>("CarriageBody");
        if (body == null) return;
        var col = body.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (col?.Shape is BoxShape3D box)
            box.Size = new Vector3(Width, Height, zLen);
    }
}
