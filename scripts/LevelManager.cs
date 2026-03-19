using Godot;
using System.Collections.Generic;

/// <summary>
/// Manages level end condition.
/// Attach to a Node child of Main.tscn called "LevelManager".
///
/// Each frame checks if player is out of turret range using TrainSpeedManager.IsPlayerOutOfRange.
/// "Out of range" = player is more than TurretRange units behind the caboose rear.
///
/// When out of range:
///   1. Shows HUD warning with 3-second countdown.
///   2. Countdown Timer starts (3 seconds).
///   3. On expire: TriggerZoomAway(), disable player input.
///   4. After 2 more seconds: change scene to AfterAction.tscn.
///
/// Targetable container check: any ContainerNode still alive on the train.
/// Level does NOT end prematurely if there are still containers to shoot.
/// (This is intentional — player might speed up to re-engage.)
/// </summary>
public partial class LevelManager : Node
{
    private const float WarningDuration = 3f;
    private const float ZoomDuration = 2f;

    private PlayerCar _playerCar = null!;
    private HUD _hud = null!;
    private TrainBuilder _trainBuilder = null!;
    private TrainSpeedManager _tsm = null!;

    private float _warningTimer = -1f;
    private float _zoomTimer = -1f;
    private bool _warningActive;
    private bool _zoomTriggered;
    private bool _cutsceneActive = true;

    public override void _Ready()
    {
        _tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _playerCar = GetParent().GetNode<PlayerCar>("PlayerCar");
        _hud = GetParent().GetNode<HUD>("HUD");
        _trainBuilder = GetParent().GetNode<TrainBuilder>("Train");

        // Position player near the front of the locomotive at start
        float startZ = _trainBuilder.LocomotiveZ - 4f;
        _playerCar.Position = new Vector3(PlayerCar.XOffset, _playerCar.YHeight, startZ);
        _playerCar.SetTrainFrontZ(_trainBuilder.LocomotiveZ);
        GD.Print($"[LevelManager] LocomotiveZ={_trainBuilder.LocomotiveZ}, CabooseZ={_trainBuilder.CabooseZ}, PlayerStart Z={startZ}");

        // Auto-beacon all Scrap containers so they appear grey from the start.
        // Pre-scanning non-Scrap containers is deferred until OnCutsceneDone()
        // so that containers appear untagged (orange) during the intro cutscene.
        foreach (var c in _trainBuilder.AllContainers)
            if (c.IsScrap) c.Tag();
    }

    /// <summary>Called by CutsceneManager when the intro cutscene finishes.</summary>
    public void OnCutsceneDone()
    {
        _cutsceneActive = false;

        var config = GetNode<GameConfig>("/root/GameConfig");
        int preScanCount = config.NumberPreScannedContainers;
        if (preScanCount > 0)
            PreScanContainers(preScanCount);

        GD.Print("[LevelManager] Cutscene done. Gameplay active.");
    }

    public override void _Process(double delta)
    {
        if (_cutsceneActive) return;

        float dt = (float)delta;

        // Update player's reference to train front
        _playerCar.SetTrainFrontZ(_trainBuilder.LocomotiveZ);

        float cabooseWorldZ = _trainBuilder.GlobalPosition.Z + _trainBuilder.CabooseZ;
        bool outOfRange = _tsm.IsPlayerOutOfRange(
            _playerCar.GlobalPosition.Z,
            cabooseWorldZ
        );

        if (outOfRange && !_warningActive && !_zoomTriggered)
        {
            StartWarning();
        }
        else if (!outOfRange && _warningActive && !_zoomTriggered)
        {
            // Player moved back into range — cancel warning
            _warningActive = false;
            _warningTimer = -1f;
            _hud.HideWarning();
        }

        if (_warningActive && !_zoomTriggered)
        {
            _warningTimer -= dt;
            _hud.UpdateCountdown(_warningTimer);
            if (_warningTimer <= 0f)
                TriggerZoom();
        }

        if (_zoomTriggered)
        {
            // Physically move the train away — uses zoom speed, not scroll speed
            _trainBuilder.Position += new Vector3(0f, 0f, _tsm.TrainZoomSpeed * dt);

            _zoomTimer -= dt;
            if (_zoomTimer <= 0f)
                EndLevel();
        }
    }

    private void PreScanContainers(int count)
    {
        // Build list of taggable (non-Scrap) containers and shuffle it
        var taggable = new List<ContainerNode>();
        foreach (var c in _trainBuilder.AllContainers)
            if (!c.IsScrap) taggable.Add(c);

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = taggable.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (taggable[i], taggable[j]) = (taggable[j], taggable[i]);
        }

        int toTag = Mathf.Min(count, taggable.Count);
        for (int i = 0; i < toTag; i++)
            taggable[i].Tag();

        GD.Print($"[LevelManager] Pre-scanned {toTag} container(s).");
    }

    private void StartWarning()
    {
        _warningActive = true;
        _warningTimer = WarningDuration;
        _hud.ShowWarning(_warningTimer);
        SoundManager.Play("cliff_warning");
        GD.Print("[LevelManager] Player out of range. Warning started.");
    }

    private void TriggerZoom()
    {
        _zoomTriggered = true;
        _warningActive = false;
        _playerCar.DisableInput();
        _tsm.TriggerZoomAway();
        _zoomTimer = ZoomDuration;
        GD.Print("[LevelManager] Zoom away triggered.");
    }

    private void EndLevel()
    {
        GD.Print("[LevelManager] Level ended. Loading AfterAction.");
        GetTree().ChangeSceneToFile("res://scenes/ui/AfterAction.tscn");
    }
}
