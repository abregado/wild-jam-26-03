using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Autoload singleton. Tracks collected cargo and game statistics across the run.
/// Subscribe ContainerNode.CargoDetached -> OnCargoDetached.
/// Subscribe ContainerNode.ContainerDestroyed -> OnContainerDestroyed.
///
/// Save-slot fields (ActiveSlot, RaidsPlayed, PlayerResources, AppliedUpgrades)
/// are populated by LoadFromSave() when a slot is chosen from the main menu.
/// WriteToSave() persists them via SaveManager.
/// </summary>
public partial class GameSession : Node
{
    [Signal] public delegate void CargoCollectedEventHandler(string cargoName);
    [Signal] public delegate void StatsChangedEventHandler();

    // ── Per-run state (reset each raid) ───────────────────────────────────────
    public Dictionary<string, int> IdentifiedCargo   { get; private set; } = new();
    public List<string>            UnidentifiedCargo  { get; private set; } = new();
    public Dictionary<string, int> CollectedCargo     { get; private set; } = new();
    public int ContainersDetached  { get; private set; }
    public int ContainersDestroyed { get; private set; }

    // ── Persistent across raids ───────────────────────────────────────────────
    public int                     ActiveSlot        { get; private set; } = -1;
    public int                     RaidsPlayed       { get; set; }
    public Dictionary<string, int> PlayerResources   { get; private set; } = new();
    public List<string>            AppliedUpgrades   { get; private set; } = new();

    // ── Cutscene tracking ─────────────────────────────────────────────────────
    /// <summary>True only on the very first raid; false for every subsequent one.</summary>
    public bool IsFirstRaid { get; private set; } = true;
    public void MarkRaidStarted() => IsFirstRaid = false;

    public override void _Ready()
    {
        Reset();
    }

    /// <summary>Clears per-raid state. Does NOT touch save-slot or persistent fields.</summary>
    public void Reset()
    {
        IdentifiedCargo.Clear();
        UnidentifiedCargo.Clear();
        CollectedCargo.Clear();
        ContainersDetached = 0;
        ContainersDestroyed = 0;
        // PlayerResources, AppliedUpgrades, RaidsPlayed, ActiveSlot — intentionally kept
    }

    /// <summary>Called by MainMenu when the player selects a used save slot.</summary>
    public void LoadFromSave(SaveManager.SlotData data, int slot)
    {
        ActiveSlot      = slot;
        RaidsPlayed     = data.RaidsPlayed;
        PlayerResources = new Dictionary<string, int>(data.Resources);
        AppliedUpgrades = new List<string>(data.Upgrades);
        IsFirstRaid     = false;

        // Re-apply saved upgrades to GameConfig
        var config = GetNode<GameConfig>("/root/GameConfig");
        foreach (var id in AppliedUpgrades)
        {
            var def = config.Upgrades.FirstOrDefault(u => u.Id == id);
            if (def != null) config.ApplyUpgrade(def);
        }

        GD.Print($"[GameSession] Loaded slot {slot}: raids={RaidsPlayed}, upgrades={AppliedUpgrades.Count}");
    }

    /// <summary>Called by MainMenu when the player starts a new game in an empty slot.</summary>
    public void StartNewGame(int slot)
    {
        ActiveSlot      = slot;
        RaidsPlayed     = 0;
        PlayerResources = new Dictionary<string, int>();
        AppliedUpgrades = new List<string>();
        IsFirstRaid     = true;
        Reset();
        GD.Print($"[GameSession] New game in slot {slot}.");
    }

    /// <summary>
    /// Increments RaidsPlayed and persists current state to the active slot.
    /// Called by AfterAction before scene reload.
    /// </summary>
    public void WriteToSave()
    {
        if (ActiveSlot < 0) return;
        RaidsPlayed++;
        GetNode<SaveManager>("/root/SaveManager").SaveSlot(ActiveSlot, this);
    }

    // ── Cargo callbacks ───────────────────────────────────────────────────────

    public void OnCargoDetached(string cargoName, bool wasBeaconed)
    {
        if (wasBeaconed)
        {
            if (!IdentifiedCargo.ContainsKey(cargoName)) IdentifiedCargo[cargoName] = 0;
            IdentifiedCargo[cargoName]++;
        }
        else
        {
            UnidentifiedCargo.Add(cargoName);
        }

        if (!CollectedCargo.ContainsKey(cargoName)) CollectedCargo[cargoName] = 0;
        CollectedCargo[cargoName]++;
        ContainersDetached++;
        EmitSignal(SignalName.CargoCollected, cargoName);
        EmitSignal(SignalName.StatsChanged);
    }

    public void OnContainerDestroyed()
    {
        ContainersDestroyed++;
        EmitSignal(SignalName.StatsChanged);
    }
}
