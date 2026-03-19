using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Plays the intro cutscene when Main.tscn first loads.
///
/// Sequence:
///   1. Initial camera sweep from behind the train to a locomotive overview.
///   2. Camera pans to each waypoint in train order (loco → caboose), showing tutorial text.
///   3. Player car appears at the front; camera sweeps to focus it.
///   4. Final text shown, then control handed to the player camera.
///
/// During the cutscene:
///   - PlayerCar is hidden and input-disabled.
///   - HUD is hidden.
///   - ObstacleSystem _Process is paused (no obstacle spawning).
///   - Mouse cursor is visible so the player can click to advance.
///
/// Player can press Space or Left Mouse Button during any text-hold phase to skip to the next
/// waypoint immediately.
///
/// Layout: focused object appears in the left half of the screen;
///         text panel occupies the right half.
///
/// Waypoint texts are read from game_config.json → "cutscene" section.
/// </summary>
public partial class CutsceneManager : Node
{
    // ── UI ─────────────────────────────────────────────────────────────────
    private Camera3D      _cam           = null!;
    private CanvasLayer   _ui            = null!;
    private Panel         _panel         = null!;
    private Label         _mainLabel     = null!;
    private Label         _continueLabel = null!;
    private RingIndicator _ring          = null!;

    // ── Scene references ───────────────────────────────────────────────────
    private TrainBuilder _trainBuilder   = null!;
    private PlayerCar    _playerCar      = null!;
    private Camera3D     _playerCamera   = null!;
    private HUD          _hud            = null!;
    private Node3D       _obstacleSystem = null!;
    private LevelManager _levelManager   = null!;

    // ── Camera tracking ────────────────────────────────────────────────────
    private Vector3 _lookTarget;
    private bool    _cutsceneRunning;

    // ── Player advance input ───────────────────────────────────────────────
    private bool _advanceRequested;

    // ── Constants ──────────────────────────────────────────────────────────
    private const float SweepTime    = 1.2f;
    private const float HoldTime     = 3.5f;
    private const float CarriageYMid = 8.75f; // TrackY(7) + CarriageHeight(2.5)/2

    // ═══════════════════════════════════════════════════════════════════════
    public override void _Ready()
    {
        // ── Cutscene camera ──────────────────────────────────────────────
        _cam = new Camera3D { Name = "CutsceneCamera", Fov = 70f };
        AddChild(_cam);
        _cam.MakeCurrent();

        // ── CanvasLayer for text + ring ──────────────────────────────────
        _ui = new CanvasLayer { Name = "CutsceneUI", Layer = 10 };
        AddChild(_ui);

        // Ring — behind the text panel so text reads clearly
        _ring = new RingIndicator { Name = "Ring", Cam = _cam, Visible = false };
        _ui.AddChild(_ring);

        // Text panel on the right half of the screen
        _panel = new Panel { Name = "TextPanel", Visible = false };
        _panel.SetAnchor(Side.Left,   0.52f);
        _panel.SetAnchor(Side.Right,  0.98f);
        _panel.SetAnchor(Side.Top,    0.30f);
        _panel.SetAnchor(Side.Bottom, 0.72f);
        _ui.AddChild(_panel);

        _mainLabel = new Label { Name = "MainLabel" };
        _mainLabel.SetAnchor(Side.Left,   0f);
        _mainLabel.SetAnchor(Side.Right,  1f);
        _mainLabel.SetAnchor(Side.Top,    0f);
        _mainLabel.SetAnchor(Side.Bottom, 0.78f);
        _mainLabel.SetOffset(Side.Left,   16f);
        _mainLabel.SetOffset(Side.Right, -16f);
        _mainLabel.SetOffset(Side.Top,    16f);
        _mainLabel.SetOffset(Side.Bottom, 0f);
        _mainLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mainLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _mainLabel.VerticalAlignment   = VerticalAlignment.Center;
        _mainLabel.AddThemeFontSizeOverride("font_size", 22);
        _panel.AddChild(_mainLabel);

        _continueLabel = new Label { Name = "ContinueLabel" };
        _continueLabel.SetAnchor(Side.Left,   0f);
        _continueLabel.SetAnchor(Side.Right,  1f);
        _continueLabel.SetAnchor(Side.Top,    0.80f);
        _continueLabel.SetAnchor(Side.Bottom, 1f);
        _continueLabel.SetOffset(Side.Left,   8f);
        _continueLabel.SetOffset(Side.Right, -8f);
        _continueLabel.SetOffset(Side.Bottom,-8f);
        _continueLabel.Text = "SPACE or click to continue";
        _continueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _continueLabel.VerticalAlignment   = VerticalAlignment.Bottom;
        _continueLabel.AddThemeFontSizeOverride("font_size", 13);
        _panel.AddChild(_continueLabel);

        // ── Scene references ─────────────────────────────────────────────
        _trainBuilder   = GetParent().GetNode<TrainBuilder>("Train");
        _playerCar      = GetParent().GetNode<PlayerCar>("PlayerCar");
        _hud            = GetParent().GetNode<HUD>("HUD");
        _obstacleSystem = GetParent().GetNode<Node3D>("ObstacleSystem");
        _levelManager   = GetParent().GetNode<LevelManager>("LevelManager");
        _playerCamera   = _playerCar.GetNode<Camera3D>("Camera3D");

        // ── Disable everything until cutscene ends ───────────────────────
        _playerCar.Visible = false;
        _playerCar.DisableInput();
        _hud.Visible = false;
        _obstacleSystem.ProcessMode = ProcessModeEnum.Disabled;

        _cutsceneRunning = true;
        _ = PlayCutscene();
    }

    // Keep camera oriented toward _lookTarget every frame while running.
    public override void _Process(double _)
    {
        if (!_cutsceneRunning) return;
        try { _cam.LookAt(_lookTarget, Vector3.Up); } catch { }
    }

    // Detect Space or LMB to advance through hold phases.
    public override void _Input(InputEvent @event)
    {
        if (!_cutsceneRunning) return;

        bool spaceDown = @event is InputEventKey k
            && k.Pressed && !k.Echo && k.Keycode == Key.Space;
        bool clickDown = @event is InputEventMouseButton mb
            && mb.Pressed && mb.ButtonIndex == MouseButton.Left;

        if (spaceDown || clickDown)
            _advanceRequested = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    private async System.Threading.Tasks.Task PlayCutscene()
    {
        // Wait one frame so all node transforms are fully propagated.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var config = GetNode<GameConfig>("/root/GameConfig");
        float locoZ    = _trainBuilder.LocomotiveZ;
        float cabooseZ = _trainBuilder.CabooseZ;

        // ── Phase 1: Initial sweep from behind the train to loco overview ─
        Vector3 startPos = new(-22f, 22f, cabooseZ - 18f);
        _cam.GlobalPosition = startPos;
        _lookTarget = new Vector3(0f, CarriageYMid, locoZ - 5f);
        try { _cam.LookAt(_lookTarget, Vector3.Up); } catch { }

        await Pause(0.1f);

        Vector3 locoViewPos    = new(-10f, 16f, locoZ + 8f);
        Vector3 locoLookTarget = new(0f, CarriageYMid + 1f, locoZ - 14f);
        _lookTarget = locoLookTarget;
        await MoveTo(locoViewPos, SweepTime * 2f);
        await Pause(0.5f);

        // ── Phase 2: Waypoints ────────────────────────────────────────────
        var waypoints = BuildWaypoints(config);
        foreach (var (camPos, lookTarget, text, ringTarget) in waypoints)
        {
            _lookTarget = lookTarget;
            await MoveTo(camPos, SweepTime);
            ShowText(text, ringTarget);
            await HoldOrAdvance(HoldTime);
            HideText();
            await Pause(0.15f);
        }

        // ── Phase 3: Reveal player car ────────────────────────────────────
        float playerZ = locoZ - 4f;
        _playerCar.Position = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        _playerCar.Visible  = true;

        // Camera moves to behind the caboose, looking toward the player car.
        Vector3 behindPos = new(0f, 16f, cabooseZ - 18f);
        _lookTarget = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        await MoveTo(behindPos, 2.0f);
        await Pause(0.2f);

        // Camera moves to focus on the player car (car in left half, camera on right side).
        Vector3 focusPos  = _playerCar.GlobalPosition + new Vector3(16f, 5f, 3f);
        Vector3 focusLook = _playerCar.GlobalPosition + new Vector3(-10f, 0f, 0f);
        _lookTarget = focusLook;
        await MoveTo(focusPos, 1.5f);

        ShowText(config.CutsceneTextFinal, _playerCar);
        await HoldOrAdvance(HoldTime);
        HideText();
        await Pause(0.2f);

        // ── Phase 4: Hand control to player ──────────────────────────────
        _cutsceneRunning = false;
        _panel.Visible   = false;

        _playerCamera.MakeCurrent();
        _playerCar.EnableInput();
        _hud.Visible = true;
        _obstacleSystem.ProcessMode = ProcessModeEnum.Inherit;

        _levelManager.OnCutsceneDone();
        GD.Print("[CutsceneManager] Cutscene complete.");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Returns (camPos, lookTarget, text, ringTarget) sorted loco → caboose.
    private List<(Vector3 camPos, Vector3 lookTarget, string text, Node3D? ringTarget)>
        BuildWaypoints(GameConfig config)
    {
        var entries = new List<(Vector3, Vector3, string, Node3D?, float sortZ)>();

        // Containers on the left side (negative X).
        var leftContainers = _trainBuilder.AllContainers
            .Where(c => c.GlobalPosition.X < 0f)
            .OrderByDescending(c => c.GlobalPosition.Z)
            .ToList();

        // Waypoint A — first left non-Scrap container
        var nonScrap = leftContainers.FirstOrDefault(c => !c.IsScrap);
        if (nonScrap != null)
        {
            var (cam, look) = CamForSide(nonScrap.GlobalPosition);
            entries.Add((cam, look, config.CutsceneTextContainer, nonScrap, nonScrap.GlobalPosition.Z));
        }

        // Waypoint B — first left Scrap container
        var scrap = leftContainers.FirstOrDefault(c => c.IsScrap);
        if (scrap != null)
        {
            var (cam, look) = CamForSide(scrap.GlobalPosition);
            entries.Add((cam, look, config.CutsceneTextScrap, scrap, scrap.GlobalPosition.Z));
        }

        // Waypoint C — topmost clamp of the first left container
        var firstLeft = leftContainers.FirstOrDefault();
        if (firstLeft != null)
        {
            var topClamp = firstLeft.Clamps
                .Where(c => c.IsAlive)
                .OrderByDescending(c => c.GlobalPosition.Y)
                .FirstOrDefault();
            if (topClamp != null)
            {
                var (cam, look) = CamForSide(topClamp.GlobalPosition);
                entries.Add((cam, look, config.CutsceneTextClamp, topClamp, topClamp.GlobalPosition.Z));
            }
        }

        // Waypoint D — first deployer (highest Z = closest to loco)
        var deployer = _trainBuilder.AllDeployers
            .OrderByDescending(d => d.GlobalPosition.Z)
            .FirstOrDefault();
        if (deployer != null)
        {
            var (cam, look) = CamForSide(deployer.GlobalPosition);
            entries.Add((cam, look, config.CutsceneTextDeployer, deployer, deployer.GlobalPosition.Z));
        }

        // Waypoint E — first roof turret
        var roofTurret = _trainBuilder.AllRoofTurrets
            .OrderByDescending(t => t.GlobalPosition.Z)
            .FirstOrDefault();
        if (roofTurret != null)
        {
            var (cam, look) = CamForSide(roofTurret.GlobalPosition);
            entries.Add((cam, look, config.CutsceneTextTurret, roofTurret, roofTurret.GlobalPosition.Z));
        }

        // Waypoint F — caboose
        if (_trainBuilder.CabooseNode != null)
        {
            Vector3 caboosePos = _trainBuilder.CabooseNode.GlobalPosition;
            var (cam, look) = CamForCaboose(caboosePos);
            entries.Add((cam, look, config.CutsceneTextCaboose, _trainBuilder.CabooseNode, caboosePos.Z));
        }

        // Sort descending by Z so the camera sweeps from locomotive toward caboose.
        entries.Sort((a, b) => b.sortZ.CompareTo(a.sortZ));
        return entries.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4)).ToList();
    }

    /// Camera positioned to the left of the target; looks rightward so the target
    /// falls in the left half of the frame.
    private static (Vector3 cam, Vector3 look) CamForSide(Vector3 target) =>
        (target + new Vector3(-12f, 5f, 2f), target + new Vector3(9f, 0f, 0f));

    /// Special framing for the caboose: camera behind and to the left.
    private static (Vector3 cam, Vector3 look) CamForCaboose(Vector3 target) =>
        (target + new Vector3(-10f, 6f, -8f), target + new Vector3(6f, 1f, 4f));

    // ───────────────────────────────────────────────────────────────────────
    private async System.Threading.Tasks.Task MoveTo(Vector3 targetPos, float duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(_cam, "global_position", targetPos, duration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// Waits up to <paramref name="maxSeconds"/> or until the player presses Space/LMB.
    private async System.Threading.Tasks.Task HoldOrAdvance(float maxSeconds)
    {
        _advanceRequested = false;
        float elapsed = 0f;
        while (!_advanceRequested && elapsed < maxSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
        }
        _advanceRequested = false;
    }

    private async System.Threading.Tasks.Task Pause(float seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void ShowText(string text, Node3D? ringTarget = null)
    {
        _mainLabel.Text  = text;
        _panel.Visible   = true;
        _ring.Target     = ringTarget;
        _ring.Visible    = ringTarget != null;
    }

    private void HideText()
    {
        _panel.Visible = false;
        _ring.Target   = null;
        _ring.Visible  = false;
    }
}
