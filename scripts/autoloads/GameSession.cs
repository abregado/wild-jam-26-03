using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Tracks collected cargo and game statistics across the run.
/// Subscribe ContainerNode.CargoDetached -> OnCargoDetached.
/// Subscribe ContainerNode.ContainerDestroyed -> OnContainerDestroyed.
/// </summary>
public partial class GameSession : Node
{
    [Signal] public delegate void CargoCollectedEventHandler(string cargoName);
    [Signal] public delegate void StatsChangedEventHandler();

    // Per-run: beaconed containers (revealed during raid)
    public Dictionary<string, int> IdentifiedCargo { get; private set; } = new();
    // Per-run: unbeaconed containers (hidden until box-breaking)
    public List<string> UnidentifiedCargo { get; private set; } = new();

    // Persistent across raids (never cleared by Reset)
    public Dictionary<string, int> PlayerResources { get; private set; } = new();

    // Legacy aggregate (kept for compatibility)
    public Dictionary<string, int> CollectedCargo { get; private set; } = new();
    public int ContainersDetached { get; private set; }
    public int ContainersDestroyed { get; private set; }

    public override void _Ready()
    {
        Reset();
    }

    public void Reset()
    {
        IdentifiedCargo.Clear();
        UnidentifiedCargo.Clear();
        CollectedCargo.Clear();
        ContainersDetached = 0;
        ContainersDestroyed = 0;
        // PlayerResources intentionally NOT cleared — persists across raids
    }

    /// <summary>Called when a container's last clamp is destroyed and cargo is auto-collected.</summary>
    public void OnCargoDetached(string cargoName, bool wasBeaconed)
    {
        if (wasBeaconed)
        {
            if (!IdentifiedCargo.ContainsKey(cargoName))
                IdentifiedCargo[cargoName] = 0;
            IdentifiedCargo[cargoName]++;
        }
        else
        {
            UnidentifiedCargo.Add(cargoName);
        }

        if (!CollectedCargo.ContainsKey(cargoName))
            CollectedCargo[cargoName] = 0;
        CollectedCargo[cargoName]++;
        ContainersDetached++;
        EmitSignal(SignalName.CargoCollected, cargoName);
        EmitSignal(SignalName.StatsChanged);
    }

    /// <summary>Called when a container is destroyed by HP damage (cargo lost).</summary>
    public void OnContainerDestroyed()
    {
        ContainersDestroyed++;
        EmitSignal(SignalName.StatsChanged);
    }
}
