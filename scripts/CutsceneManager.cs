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
    private Vector3 _desiredLook;   // where we want to look (set instantly)
    private Vector3 _smoothLook;    // actual LookAt target (lerped each frame)
    private bool    _cutsceneRunning;

    private const float LookLerpSpeed = 2.8f; // roughly 0.5 s to cover most of the rotation

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
        var session = GetNode<GameSession>("/root/GameSession");
        if (session.IsFirstRaid)
        {
            session.MarkRaidStarted();
            _ = PlayCutscene();
        }
        else
        {
            _ = PlayFlyIn();
        }
    }

    // Smoothly track the desired look target every frame.
    public override void _Process(double delta)
    {
        if (!_cutsceneRunning) return;
        _smoothLook = _smoothLook.Lerp(_desiredLook, (float)delta * LookLerpSpeed);
        try { _cam.LookAt(_smoothLook, Vector3.Up); } catch { }
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
        // Seed both fields so _Process doesn't lerp in from Vector3.Zero.
        _desiredLook = new Vector3(0f, CarriageYMid, locoZ - 5f);
        _smoothLook  = _desiredLook;
        try { _cam.LookAt(_desiredLook, Vector3.Up); } catch { }

        await Pause(0.1f);

        Vector3 locoViewPos    = new(-10f, 16f, locoZ + 8f);
        Vector3 locoLookTarget = new(0f, CarriageYMid + 1f, locoZ - 14f);
        _desiredLook = locoLookTarget;
        await MoveTo(locoViewPos, SweepTime * 2f);
        await Pause(0.5f);

        // ── Phase 2: Waypoints ────────────────────────────────────────────
        // Setting _desiredLook before MoveTo means the camera rotates toward the
        // new target *while* it is already travelling — the smooth lerp in _Process
        // blends the rotation so there is no snap between waypoints.
        var waypoints = BuildWaypoints(config);
        foreach (var (camPos, lookTarget, text, ringTarget, worldRadius) in waypoints)
        {
            _desiredLook = lookTarget;
            await MoveTo(camPos, SweepTime);
            ShowText(text, ringTarget, worldRadius);
            await HoldOrAdvance(HoldTime);
            HideText();
        }

        // ── Phase 3: Reveal player car ────────────────────────────────────
        float playerZ = locoZ - 4f;
        _playerCar.Position = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        _playerCar.Visible  = true;

        // Camera moves to behind the caboose, looking toward the player car.
        Vector3 behindPos = new(0f, 16f, cabooseZ - 18f);
        _desiredLook = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        await MoveTo(behindPos, 2.0f);
        await Pause(0.2f);

        // Camera moves to focus on the player car (car in left half of the screen).
        var (focusPos, focusLook) = ComputeFraming(
            _playerCar.GlobalPosition, new Vector3(16f, 5f, 3f), _cam.Fov);
        _desiredLook = focusLook;
        await MoveTo(focusPos, 1.5f);

        ShowText(config.CutsceneTextFinal, _playerCar, 1.5f);
        await HoldOrAdvance(HoldTime);
        HideText();
        await Pause(0.2f);

        // ── Blend into player camera ──────────────────────────────────────
        // Tween to the exact position/orientation of the player camera so the
        // switch to _playerCamera.MakeCurrent() is seamless.
        Vector3 playerCamPos        = _playerCamera.GlobalPosition;
        Vector3 playerCamLookTarget = playerCamPos - _playerCamera.GlobalBasis.Z * 20f;
        _desiredLook = playerCamLookTarget;
        await MoveTo(playerCamPos, 1.2f);
        _smoothLook = playerCamLookTarget; // eliminate residual lerp error

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

    // ═══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Fly-in for raids 2+: sweeps from behind the caboose, arcs over the train,
    /// then descends to the player camera's position and blends into it.
    /// No text, no waypoints — pure cinematic camera move.
    /// </summary>
    private async System.Threading.Tasks.Task PlayFlyIn()
    {
        // Wait one frame so all node transforms are fully propagated.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        float locoZ    = _trainBuilder.LocomotiveZ;
        float cabooseZ = _trainBuilder.CabooseZ;

        // Place the player car immediately so _playerCamera.GlobalTransform is valid.
        float playerZ = locoZ - 4f;
        _playerCar.Position = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        _playerCar.Visible  = true;

        // Wait one more frame for the player car's children to inherit the new transform.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // Snapshot player camera world transform before any cutscene camera movement.
        Vector3 playerCamPos = _playerCamera.GlobalPosition;
        // Look target that matches the player camera's viewing direction.
        Vector3 playerCamLookTarget = playerCamPos - _playerCamera.GlobalBasis.Z * 20f;

        // ── Phase 1: Start behind and above the caboose ───────────────────
        Vector3 startPos = new(-22f, 22f, cabooseZ - 18f);
        _cam.GlobalPosition = startPos;
        _desiredLook = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        _smoothLook  = _desiredLook;
        try { _cam.LookAt(_desiredLook, Vector3.Up); } catch { }

        await Pause(0.1f);

        // ── Phase 2: Arc over the middle of the train ─────────────────────
        // Camera rises up and crosses from behind the train to the right side.
        float midZ    = (locoZ + cabooseZ) * 0.5f;
        Vector3 arcPos = new(4f, 26f, midZ);
        _desiredLook = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, playerZ);
        await MoveTo(arcPos, 2.2f);

        // ── Phase 3: Descend toward the player camera ─────────────────────
        // Gradually rotate to align with what the player camera sees.
        _desiredLook = playerCamLookTarget;
        await MoveTo(playerCamPos, 2.0f);

        // Snap look exactly so there is no residual rotation error at handover.
        _smoothLook = playerCamLookTarget;

        // ── Phase 4: Hand control to player ──────────────────────────────
        _cutsceneRunning = false;

        _playerCamera.MakeCurrent();
        _playerCar.EnableInput();
        _hud.Visible = true;
        _obstacleSystem.ProcessMode = ProcessModeEnum.Inherit;

        _levelManager.OnCutsceneDone();
        GD.Print("[CutsceneManager] Fly-in complete.");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Returns (camPos, lookTarget, text, ringTarget, worldRadius) sorted loco → caboose.
    private List<(Vector3 camPos, Vector3 lookTarget, string text, Node3D? ringTarget, float worldRadius)>
        BuildWaypoints(GameConfig config)
    {
        var entries = new List<(Vector3, Vector3, string, Node3D?, float worldRadius, float sortZ)>();
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        // Containers on the left side (negative X), front-to-back order.
        var leftContainers = _trainBuilder.AllContainers
            .Where(c => c.GlobalPosition.X < 0f)
            .OrderByDescending(c => c.GlobalPosition.Z)
            .ToList();

        // Waypoint A — first left non-Scrap container (highest Z).
        var nonScrap = leftContainers.FirstOrDefault(c => !c.IsScrap);
        if (nonScrap != null)
        {
            var (cam, look) = ComputeFraming(nonScrap.GlobalPosition, new Vector3(-12f, 5f, 2f), _cam.Fov);
            entries.Add((cam, look, config.CutsceneTextContainer, nonScrap, 1.5f, nonScrap.GlobalPosition.Z));
        }

        // Waypoint B — first left Scrap container that comes *after* (lower Z than) Waypoint A.
        float nonScrapZ  = nonScrap?.GlobalPosition.Z ?? float.MaxValue;
        var   scrap      = leftContainers.FirstOrDefault(c => c.IsScrap && c.GlobalPosition.Z < nonScrapZ);
        // Fallback: any scrap on the left if none are behind Waypoint A.
        scrap ??= leftContainers.FirstOrDefault(c => c.IsScrap && c != nonScrap);
        if (scrap != null)
        {
            var (cam, look) = ComputeFraming(scrap.GlobalPosition, new Vector3(-12f, 5f, 2f), _cam.Fov);
            entries.Add((cam, look, config.CutsceneTextScrap, scrap, 1.5f, scrap.GlobalPosition.Z));
        }

        // Waypoint C — topmost clamp of the first left container.
        var firstLeft = leftContainers.FirstOrDefault();
        if (firstLeft != null)
        {
            var topClamp = firstLeft.Clamps
                .Where(c => c.IsAlive)
                .OrderByDescending(c => c.GlobalPosition.Y)
                .FirstOrDefault();
            if (topClamp != null)
            {
                var (cam, look) = ComputeFraming(topClamp.GlobalPosition, new Vector3(-12f, 5f, 2f), _cam.Fov);
                entries.Add((cam, look, config.CutsceneTextClamp, topClamp, 0.5f, topClamp.GlobalPosition.Z));
            }
        }

        // Waypoint D — random deployer.
        var deployers = _trainBuilder.AllDeployers;
        if (deployers.Count > 0)
        {
            var deployer = deployers[rng.RandiRange(0, deployers.Count - 1)];
            var (cam, look) = ComputeFraming(deployer.GlobalPosition, new Vector3(-12f, 5f, 2f), _cam.Fov);
            entries.Add((cam, look, config.CutsceneTextDeployer, deployer, 0.7f, deployer.GlobalPosition.Z));
        }

        // Waypoint E — random roof turret.
        var roofTurrets = _trainBuilder.AllRoofTurrets;
        if (roofTurrets.Count > 0)
        {
            var roofTurret = roofTurrets[rng.RandiRange(0, roofTurrets.Count - 1)];
            var (cam, look) = ComputeFraming(roofTurret.GlobalPosition, new Vector3(-12f, 5f, 2f), _cam.Fov);
            entries.Add((cam, look, config.CutsceneTextTurret, roofTurret, 0.7f, roofTurret.GlobalPosition.Z));
        }

        // Waypoint F — caboose.
        if (_trainBuilder.CabooseNode != null)
        {
            Vector3 caboosePos = _trainBuilder.CabooseNode.GlobalPosition;
            var (cam, look) = ComputeFraming(caboosePos, new Vector3(-10f, 6f, -8f), _cam.Fov);
            entries.Add((cam, look, config.CutsceneTextCaboose, _trainBuilder.CabooseNode, 4.5f, caboosePos.Z));
        }

        // Sort descending by Z so the camera sweeps from locomotive toward caboose.
        entries.Sort((a, b) => b.sortZ.CompareTo(a.sortZ));
        return entries.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, e.Item5)).ToList();
    }

    /// Positions the camera at <paramref name="target"/> + <paramref name="camOffset"/> and computes
    /// a look-at point so the target lands at (0.25 W, 0.5 H) — the centre of the left half.
    private static (Vector3 cam, Vector3 look) ComputeFraming(Vector3 target, Vector3 camOffset, float fovDeg)
    {
        Vector3 camPos  = target + camOffset;
        Vector3 toT     = target - camPos;
        float   dist    = toT.Length();
        Vector3 forward = toT / dist;
        // Godot LookAt convention: right = normalize(Up × -forward) — same cross as camera's basis.X.
        Vector3 camRight = Vector3.Up.Cross(-forward).Normalized();
        float   halfFov  = Mathf.DegToRad(fovDeg * 0.5f);
        // Shift look point rightward by half a half-screen so target sits at x=0.25 of full width.
        Vector3 lookAt   = target + camRight * (0.5f * Mathf.Tan(halfFov) * dist);
        return (camPos, lookAt);
    }

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

    private void ShowText(string text, Node3D? ringTarget = null, float worldRadius = 1.5f)
    {
        _mainLabel.Text      = text;
        _panel.Visible       = true;
        _ring.WorldRadius    = worldRadius;
        _ring.Target         = ringTarget;
        _ring.Visible        = ringTarget != null;
    }

    private void HideText()
    {
        _panel.Visible = false;
        _ring.Target   = null;
        _ring.Visible  = false;
    }
}
