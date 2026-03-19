using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Reads and writes the three save slots stored in user://saves/.
///
/// Each slot file (slot_0.json, slot_1.json, slot_2.json) contains:
///   raids_played  int
///   last_save     string  (ISO date "YYYY-MM-DD")
///   resources     dict    (cargo name → count)
///   upgrades      array   (upgrade id strings)
/// </summary>
public partial class SaveManager : Node
{
    private const string SaveDir = "user://saves";

    public class SlotData
    {
        public int RaidsPlayed { get; set; }
        public string LastSave { get; set; } = "";
        public Dictionary<string, int> Resources { get; set; } = new();
        public List<string> Upgrades { get; set; } = new();
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
    }

    private static string SlotPath(int slot) => $"{SaveDir}/slot_{slot}.json";

    public bool SlotExists(int slot) => FileAccess.FileExists(SlotPath(slot));

    public SlotData? LoadSlot(int slot)
    {
        string path = SlotPath(slot);
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok) return null;

        var d = json.Data.AsGodotDictionary();
        var data = new SlotData();
        if (d.TryGetValue("raids_played", out var rp)) data.RaidsPlayed = rp.AsInt32();
        if (d.TryGetValue("last_save",    out var ls)) data.LastSave    = ls.AsString();

        if (d.TryGetValue("resources", out var resV))
            foreach (var kv in resV.AsGodotDictionary())
                data.Resources[kv.Key.AsString()] = kv.Value.AsInt32();

        if (d.TryGetValue("upgrades", out var upV))
            foreach (var item in upV.AsGodotArray())
                data.Upgrades.Add(item.AsString());

        return data;
    }

    public void SaveSlot(int slot, GameSession session)
    {
        var resources = new Godot.Collections.Dictionary();
        foreach (var kv in session.PlayerResources)
            resources[kv.Key] = kv.Value;

        var upgrades = new Godot.Collections.Array();
        foreach (var id in session.AppliedUpgrades)
            upgrades.Add(id);

        var data = new Godot.Collections.Dictionary
        {
            ["raids_played"] = session.RaidsPlayed,
            ["last_save"]    = DateTime.Now.ToString("yyyy-MM-dd"),
            ["resources"]    = resources,
            ["upgrades"]     = upgrades,
        };

        string path = SlotPath(slot);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[SaveManager] Cannot write {path}");
            return;
        }
        file.StoreString(Json.Stringify(data));
        GD.Print($"[SaveManager] Slot {slot} saved (raids={session.RaidsPlayed}).");
    }

    public void DeleteSlot(int slot)
    {
        string path = SlotPath(slot);
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(path);
        GD.Print($"[SaveManager] Slot {slot} deleted.");
    }

    /// <summary>Returns basic display info without fully loading the slot.</summary>
    public (int raids, string date) GetSlotMeta(int slot)
    {
        var data = LoadSlot(slot);
        return data == null ? (0, "") : (data.RaidsPlayed, data.LastSave);
    }
}
