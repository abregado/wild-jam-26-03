using Godot;
using System.Collections.Generic;

/// <summary>
/// Script for a train carriage node.
/// Holds the list of container children for reference.
/// MeshSlot child is the BoxMesh — swap it for a real model later.
/// </summary>
public partial class Carriage : Node3D
{
    public List<ContainerNode> Containers { get; } = new();
    public List<DeployerNode> Deployers { get; } = new();

    public override void _Ready()
    {
        // Collect any ContainerNode children added by TrainBuilder
        foreach (Node child in GetChildren())
        {
            if (child is ContainerNode c)
                Containers.Add(c);
        }
    }
}
