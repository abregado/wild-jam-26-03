using Godot;

/// <summary>
/// After-action screen. Reads GameSession.CollectedCargo and populates the results.
/// "Play Again" button: resets session + speed manager, changes scene back to Main.tscn.
/// </summary>
public partial class AfterAction : Control
{
    [Export] public NodePath CargoListPath { get; set; } = "Panel/VBox/CargoList";
    [Export] public NodePath PlayAgainPath { get; set; } = "Panel/VBox/PlayAgainButton";
    [Export] public NodePath SummaryLabelPath { get; set; } = "Panel/VBox/SummaryLabel";

    public override void _Ready()
    {
        var session = GetNode<GameSession>("/root/GameSession");
        var list = GetNode<VBoxContainer>(CargoListPath);
        var playAgainBtn = GetNode<Button>(PlayAgainPath);
        var summaryLabel = GetNode<Label>(SummaryLabelPath);

        // Summary
        summaryLabel.Text = $"Cargo Detached: {session.ContainersDetached}   Lost: {session.ContainersDestroyed}";

        // Populate cargo rows
        foreach (Node child in list.GetChildren())
            child.QueueFree();

        var config = GetNode<GameConfig>("/root/GameConfig");
        foreach (var cargoType in config.CargoTypes)
        {
            int count = session.CollectedCargo.TryGetValue(cargoType.Name, out var c) ? c : 0;
            var label = new Label
            {
                Text = $"{cargoType.Name}: {count}",
                Modulate = count > 0 ? cargoType.Color : Colors.Gray
            };
            list.AddChild(label);
        }

        playAgainBtn.Pressed += OnPlayAgain;

        // Release mouse
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnPlayAgain()
    {
        var session = GetNode<GameSession>("/root/GameSession");
        var tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        session.Reset();
        tsm.ResetSpeed();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }
}
