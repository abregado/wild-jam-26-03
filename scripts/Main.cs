using Godot;

/// <summary>
/// Main scene script. Entry point for gameplay.
/// Wires up HUD references to PlayerCar and Turret after all children are ready.
/// No main menu — starts directly into gameplay.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        // Wire HUD to PlayerCar and Turret
        var hud = GetNode<HUD>("HUD");
        var playerCar = GetNode<PlayerCar>("PlayerCar");
        var turret = playerCar.GetNode<Turret>("Turret");
        hud.SetReferences(playerCar, turret);

        MusicManager.PlayContext("raid");
        GD.Print("[Main] Scene ready. Game starting.");
    }
}
