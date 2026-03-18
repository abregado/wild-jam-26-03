using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// After-action phase state machine:
///   BoxBreak → ResourceFly → Purchase (cards)
///
/// BOX_BREAK: Unidentified containers shown one at a time in a 3-D SubViewport.
///   Click 4 times to break each open. Each click jiggles the mesh.
///   Final click plays a crack animation, reveals the cargo type.
///
/// RESOURCE_FLY: All gathered resources shown in centre.
///   After 1s each label flies to the top ResourceCounter and increments it.
///
/// PURCHASE: Three upgrade cards slide in and flip to reveal.
///   Player buys upgrades, then clicks Next Raid.
/// </summary>
public partial class AfterAction : Control
{
    private enum Phase { BoxBreak, ResourceFly, Purchase }
    private Phase _phase = Phase.BoxBreak;

    private GameSession _session = null!;
    private GameConfig  _config  = null!;

    // ── Top bar ───────────────────────────────────────────────────────────────
    private HBoxContainer _resourceLabels = null!;

    // ── Shared ────────────────────────────────────────────────────────────────
    private Label         _phaseLabel     = null!;
    private VBoxContainer _boxBreakArea   = null!;
    private VBoxContainer _haulList       = null!;
    private HBoxContainer _cardRow        = null!;
    private Button        _nextRaidButton = null!;

    // ── Box-break 3-D state ───────────────────────────────────────────────────
    private const int ClicksRequired = 4;
    private int  _currentBoxIndex  = 0;
    private int  _clicksOnCurrent  = 0;
    private bool _isAnimating      = false;

    private Node3D            _containerAutoRotate = null!;   // slow Y rotation
    private Node3D            _containerPivot      = null!;   // jiggle Z tween
    private MeshInstance3D    _meshInstance        = null!;
    private StandardMaterial3D _containerMaterial  = null!;
    private SubViewportContainer _viewportContainer = null!;
    private Label _clickPromptLabel  = null!;
    private Label _progressDotsLabel = null!;
    private Label _crackRevealLabel  = null!;
    private Label _boxCountLabel     = null!;

    // ── Cards ─────────────────────────────────────────────────────────────────
    private readonly List<UpgradeCard> _cards = new();

    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameSession");
        _config  = GetNode<GameConfig>("/root/GameConfig");

        _resourceLabels  = GetNode<HBoxContainer>("ResourceCounter/VBox/ResourceLabels");
        _phaseLabel      = GetNode<Label>("MainContent/PhaseLabel");
        _boxBreakArea    = GetNode<VBoxContainer>("MainContent/BoxBreakArea");
        _haulList        = GetNode<VBoxContainer>("MainContent/HaulList");
        _cardRow         = GetNode<HBoxContainer>("MainContent/CardRow");
        _nextRaidButton  = GetNode<Button>("MainContent/NextRaidButton");

        _nextRaidButton.Pressed += OnNextRaid;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        BuildResourceCounter();

        if (_session.UnidentifiedCargo.Count == 0)
            StartResourceFly();
        else
            StartBoxBreak();
    }

    public override void _Process(double delta)
    {
        if (_phase == Phase.BoxBreak
            && _containerAutoRotate != null
            && IsInstanceValid(_containerAutoRotate))
        {
            _containerAutoRotate.RotateY((float)delta * 1.1f);
        }
    }

    // ── Resource Counter ──────────────────────────────────────────────────────

    private void BuildResourceCounter()
    {
        foreach (Node child in _resourceLabels.GetChildren())
            child.QueueFree();

        bool first = true;
        foreach (var ct in _config.CargoTypes)
        {
            if (!first)
            {
                _resourceLabels.AddChild(new Label { Text = "  |  " });
            }
            first = false;

            int amount = _session.PlayerResources.TryGetValue(ct.Name, out var v) ? v : 0;
            var lbl = new Label
            {
                Name     = $"Res_{ct.Name}",
                Text     = $"{ct.Name}: {amount}",
                Modulate = ct.Color,
            };
            _resourceLabels.AddChild(lbl);
        }
    }

    private void UpdateResourceLabel(string name)
    {
        int amount = _session.PlayerResources.TryGetValue(name, out var v) ? v : 0;
        var lbl = _resourceLabels.GetNodeOrNull<Label>($"Res_{name}");
        if (lbl != null)
            lbl.Text = $"{name}: {amount}";
    }

    // ── Phase: BOX_BREAK ──────────────────────────────────────────────────────

    private void StartBoxBreak()
    {
        _phase = Phase.BoxBreak;
        _phaseLabel.Text = "BREAK OPEN CARGO";
        _boxBreakArea.Visible = true;
        _haulList.Visible     = false;
        _cardRow.Visible      = false;

        Build3DViewport();

        // "Box X of Y" counter
        _boxCountLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _boxBreakArea.AddChild(_boxCountLabel);

        _clickPromptLabel = new Label
        {
            Text = "Click to break open!",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _boxBreakArea.AddChild(_clickPromptLabel);

        _progressDotsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _progressDotsLabel.AddThemeFontSizeOverride("font_size", 18);
        _boxBreakArea.AddChild(_progressDotsLabel);

        _crackRevealLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _crackRevealLabel.AddThemeFontSizeOverride("font_size", 36);
        _boxBreakArea.AddChild(_crackRevealLabel);

        LoadBox(0);
    }

    private void Build3DViewport()
    {
        _viewportContainer = new SubViewportContainer
        {
            Stretch     = true,
            CustomMinimumSize = new Vector2(260, 260),
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };

        var sv = new SubViewport
        {
            Size = new Vector2I(260, 260),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        _viewportContainer.AddChild(sv);

        // Camera
        var cam = new Camera3D();
        cam.Position = new Vector3(0f, 0.4f, 3.8f);
        cam.LookAt(Vector3.Zero);
        sv.AddChild(cam);

        // Key light
        var key = new DirectionalLight3D();
        key.RotationDegrees = new Vector3(-40f, 30f, 0f);
        key.LightEnergy = 1.4f;
        sv.AddChild(key);

        // Fill light
        var fill = new OmniLight3D();
        fill.Position = new Vector3(-3f, 2f, 3f);
        fill.LightEnergy = 0.6f;
        fill.OmniRange = 15f;
        sv.AddChild(fill);

        // Container hierarchy: auto-rotate parent → jiggle pivot → mesh
        _containerAutoRotate = new Node3D();
        sv.AddChild(_containerAutoRotate);

        _containerPivot = new Node3D();
        _containerAutoRotate.AddChild(_containerPivot);

        _meshInstance = new MeshInstance3D();
        _containerPivot.AddChild(_meshInstance);

        // Load GLB mesh or fall back to box primitive
        Mesh? mesh = null;
        var glbScene = GD.Load<PackedScene>("res://assets/models/train/container.glb");
        if (glbScene != null)
        {
            var root = glbScene.Instantiate<Node3D>();
            var body = root.GetNodeOrNull<MeshInstance3D>("Body");
            if (body != null) mesh = body.Mesh;
            root.QueueFree();
        }
        mesh ??= new BoxMesh { Size = new Vector3(2f, 2f, 3f) };
        _meshInstance.Mesh = mesh;

        _containerMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.5f, 0.05f),
        };
        _meshInstance.MaterialOverride = _containerMaterial;

        _viewportContainer.GuiInput += OnViewportInput;
        _boxBreakArea.AddChild(_viewportContainer);
    }

    private void LoadBox(int index)
    {
        _currentBoxIndex  = index;
        _clicksOnCurrent  = 0;
        _isAnimating      = false;

        // Reset 3-D pose
        _containerAutoRotate.Rotation = Vector3.Zero;
        _containerPivot.Rotation      = Vector3.Zero;
        _containerPivot.Scale         = Vector3.One;

        // Reset material to unknown orange
        _containerMaterial.AlbedoColor        = new Color(0.95f, 0.5f, 0.05f);
        _containerMaterial.EmissionEnabled    = false;

        _viewportContainer.Visible = true;
        _clickPromptLabel.Visible  = true;
        _progressDotsLabel.Visible = true;
        _crackRevealLabel.Visible  = false;

        int total = _session.UnidentifiedCargo.Count;
        _boxCountLabel.Text = $"Container {index + 1} / {total}";
        UpdateDots();
    }

    private void UpdateDots()
    {
        var s = "";
        for (int i = 0; i < ClicksRequired; i++)
            s += i < _clicksOnCurrent ? "● " : "○ ";
        _progressDotsLabel.Text = s.TrimEnd();
    }

    private void OnViewportInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex != MouseButton.Left || !mb.Pressed) return;
        if (_isAnimating || _phase != Phase.BoxBreak) return;

        _clicksOnCurrent++;
        UpdateDots();

        if (_clicksOnCurrent >= ClicksRequired)
            PlayCrack();
        else
            PlayJiggle(intense: false);
    }

    private void PlayJiggle(bool intense)
    {
        _isAnimating = true;
        float a = intense ? 18f : 11f;
        var t = CreateTween();
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  a,        0.04f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z", -a,        0.07f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  a * 0.5f, 0.05f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z", -a * 0.3f, 0.05f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  0f,       0.06f);
        t.Finished += () => _isAnimating = false;
    }

    private void PlayCrack()
    {
        _isAnimating = true;
        string cargoName = _session.UnidentifiedCargo[_currentBoxIndex];
        Color  cargoColor = _config.CargoTypes.FirstOrDefault(c => c.Name == cargoName)?.Color
                            ?? Colors.White;

        // Reveal colour on mesh before scale-out
        _containerMaterial.AlbedoColor           = cargoColor;
        _containerMaterial.EmissionEnabled       = true;
        _containerMaterial.Emission              = cargoColor;
        _containerMaterial.EmissionEnergyMultiplier = 2f;

        var t = CreateTween();
        // Intense shake
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  25f, 0.04f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z", -25f, 0.04f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  20f, 0.03f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z", -20f, 0.03f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z",  15f, 0.03f);
        t.TweenProperty(_containerPivot, "rotation_degrees:z", -15f, 0.03f);
        // Scale to zero
        t.TweenProperty(_containerPivot, "scale", Vector3.Zero, 0.18f)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        t.TweenCallback(Callable.From(() => ShowReveal(cargoName, cargoColor)));
    }

    private void ShowReveal(string cargoName, Color cargoColor)
    {
        _viewportContainer.Visible = false;
        _progressDotsLabel.Visible = false;
        _clickPromptLabel.Visible  = false;

        _crackRevealLabel.Text     = cargoName;
        _crackRevealLabel.Modulate = cargoColor;
        _crackRevealLabel.Visible  = true;

        // Brief flash
        var flash = CreateTween();
        flash.TweenProperty(_crackRevealLabel, "modulate", Colors.White * 2.5f, 0.06f);
        flash.TweenProperty(_crackRevealLabel, "modulate", cargoColor, 0.25f);

        GetTree().CreateTimer(0.85f).Timeout += AdvanceBox;
    }

    private void AdvanceBox()
    {
        int next = _currentBoxIndex + 1;
        if (next < _session.UnidentifiedCargo.Count)
            LoadBox(next);
        else
            GetTree().CreateTimer(0.2f).Timeout += StartResourceFly;
    }

    // ── Phase: RESOURCE_FLY ───────────────────────────────────────────────────

    private void StartResourceFly()
    {
        _phase = Phase.ResourceFly;
        _phaseLabel.Text      = "RESOURCES SECURED";
        _boxBreakArea.Visible = false;
        _haulList.Visible     = true;
        _cardRow.Visible      = false;

        // Build combined haul map
        var combined = BuildCombinedHaul();

        // Show totals centred in screen
        foreach (Node child in _haulList.GetChildren())
            child.QueueFree();

        if (combined.Count == 0)
        {
            var empty = new Label
            {
                Text = "No cargo collected.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = Colors.Gray,
            };
            _haulList.AddChild(empty);
        }
        else
        {
            foreach (var kv in combined)
            {
                var color = _config.CargoTypes.FirstOrDefault(c => c.Name == kv.Key)?.Color ?? Colors.White;
                var lbl = new Label
                {
                    Name = $"Haul_{kv.Key}",
                    Text = $"{kv.Value}×  {kv.Key}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Modulate = color,
                };
                lbl.AddThemeFontSizeOverride("font_size", 22);
                lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _haulList.AddChild(lbl);
            }
        }

        // Wait for layout to settle, then fly
        GetTree().CreateTimer(1.0f).Timeout += AnimateResourceFly;
    }

    private void AnimateResourceFly()
    {
        var combined = BuildCombinedHaul();

        if (combined.Count == 0)
        {
            GetTree().CreateTimer(0.3f).Timeout += StartCardsIn;
            return;
        }

        int i = 0;
        foreach (var kv in combined)
        {
            float delay      = i * 0.28f;
            string cargoName = kv.Key;
            int    count     = kv.Value;
            var    color     = _config.CargoTypes.FirstOrDefault(c => c.Name == cargoName)?.Color ?? Colors.White;

            var sourceLabel = _haulList.GetNodeOrNull<Label>($"Haul_{cargoName}");
            if (sourceLabel == null) { i++; continue; }

            // Source position (centre of the haul label)
            var srcPos = sourceLabel.GlobalPosition + sourceLabel.Size * 0.5f;

            // Target position (centre of the counter label)
            var targetLabel = _resourceLabels.GetNodeOrNull<Label>($"Res_{cargoName}");
            var dstPos = targetLabel != null
                ? targetLabel.GlobalPosition + targetLabel.Size * 0.5f
                : new Vector2(GetViewportRect().Size.X * 0.5f, 30f);

            // Fade out the haul label
            var fadeTween = CreateTween();
            fadeTween.TweenInterval(delay * 0.4f);
            fadeTween.TweenProperty(sourceLabel, "modulate:a", 0f, 0.25f);

            // Create flying clone directly on AfterAction (so it can leave the layout)
            var flyLbl = new Label
            {
                Text     = $"{count}× {cargoName}",
                Modulate = color,
            };
            flyLbl.AddThemeFontSizeOverride("font_size", 18);
            AddChild(flyLbl);
            // Position after AddChild so the node is in the tree
            flyLbl.GlobalPosition = srcPos - new Vector2(40f, 10f);

            string cap  = cargoName;
            int    capC = count;
            var    capT = targetLabel;

            var flyTween = CreateTween();
            flyTween.TweenInterval(delay);
            flyTween.TweenProperty(flyLbl, "global_position", dstPos - new Vector2(40f, 10f), 0.45f)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            flyTween.TweenCallback(Callable.From(() =>
            {
                // Accumulate resource
                _session.PlayerResources.TryGetValue(cap, out var existing);
                _session.PlayerResources[cap] = existing + capC;
                UpdateResourceLabel(cap);

                // Pulse the counter label
                if (capT != null)
                {
                    var pulse = CreateTween();
                    pulse.TweenProperty(capT, "scale", new Vector2(1.35f, 1.35f), 0.07f);
                    pulse.TweenProperty(capT, "scale", Vector2.One, 0.16f);
                }

                flyLbl.QueueFree();
            }));

            i++;
        }

        float totalDelay = combined.Count * 0.28f + 0.7f;
        GetTree().CreateTimer(totalDelay).Timeout += StartCardsIn;
    }

    // ── Phase: PURCHASE (cards slide in) ──────────────────────────────────────

    private void StartCardsIn()
    {
        _phase = Phase.Purchase;
        _phaseLabel.Text = "CHOOSE UPGRADE";
        _haulList.Visible = false;
        _cardRow.Visible  = true;
        _nextRaidButton.Disabled = false;

        var affordable = _config.Upgrades
            .Where(CanAfford)
            .OrderBy(_ => GD.Randf())
            .Take(3)
            .ToList();

        for (int i = 0; i < 3; i++)
        {
            UpgradeDefinition? def = i < affordable.Count ? affordable[i] : null;
            var card = new UpgradeCard(def, this);
            card.CustomMinimumSize = new Vector2(210, 230);
            _cardRow.AddChild(card);
            _cards.Add(card);

            // Slide in from below (staggered)
            card.Position += new Vector2(0, 320);
            float slideDelay = i * 0.13f;
            var slideTween = CreateTween();
            slideTween.TweenInterval(slideDelay);
            slideTween.TweenProperty(card, "position:y", card.Position.Y - 320f, 0.42f)
                       .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

            // Flip to reveal (after slide settles)
            float flipDelay = slideDelay + 0.42f + i * 0.08f;
            int ci = i;
            var flipTween = CreateTween();
            flipTween.TweenInterval(flipDelay);
            flipTween.TweenProperty(card, "scale:y", 0f, 0.14f);
            flipTween.TweenCallback(Callable.From(() => _cards[ci].Reveal()));
            flipTween.TweenProperty(card, "scale:y", 1f, 0.14f);
        }
    }

    // ── Upgrade Purchase ──────────────────────────────────────────────────────

    public bool CanAfford(UpgradeDefinition u)
    {
        foreach (var kv in u.Cost)
        {
            _session.PlayerResources.TryGetValue(kv.Key, out var have);
            if (have < kv.Value) return false;
        }
        return true;
    }

    public void PurchaseUpgrade(UpgradeDefinition u, UpgradeCard card)
    {
        if (!CanAfford(u)) return;
        foreach (var kv in u.Cost)
        {
            _session.PlayerResources[kv.Key] -= kv.Value;
            UpdateResourceLabel(kv.Key);
        }
        _config.ApplyUpgrade(u);
        card.MarkPurchased();
        foreach (var c in _cards)
            c.RefreshAffordability();
    }

    // ── Next Raid ─────────────────────────────────────────────────────────────

    private void OnNextRaid()
    {
        _session.Reset();
        GetNode<TrainSpeedManager>("/root/TrainSpeedManager").ResetSpeed();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Dictionary<string, int> BuildCombinedHaul()
    {
        var combined = new Dictionary<string, int>();
        foreach (var kv in _session.IdentifiedCargo)
            combined[kv.Key] = kv.Value;
        foreach (var name in _session.UnidentifiedCargo)
        {
            combined.TryGetValue(name, out var cur);
            combined[name] = cur + 1;
        }
        return combined;
    }
}

// ── UpgradeCard ───────────────────────────────────────────────────────────────

public partial class UpgradeCard : PanelContainer
{
    // Per-stat metadata: (human-readable name, whether a positive delta is beneficial).
    // If positive is BAD (e.g. reload_time — longer is worse) set false.
    private static readonly Dictionary<string, (string Label, bool PosIsGood)> StatMeta = new()
    {
        ["turret_tracking_speed"]    = ("Tracking Speed",   true),
        ["turret_damage"]            = ("Damage",           true),
        ["burst_count"]              = ("Burst Count",      true),
        ["burst_delay"]              = ("Burst Delay",      false),   // lower = faster
        ["rate_of_fire"]             = ("Rate of Fire",     true),
        ["bullet_speed"]             = ("Bullet Speed",     true),
        ["beacon_reload_speed"]      = ("Beacon Reload",    false),   // lower = faster
        ["max_relative_velocity"]    = ("Max Speed",        true),
        ["blast_radius"]             = ("Blast Radius",     true),
        ["shield_block_angle"]       = ("Shield Angle",     true),
        ["car_speed_damage_per_hit"] = ("Speed Dmg / Hit",  false),   // lower = less damage taken
        ["number_pre_scanned_containers"] = ("Pre-Scanned Containers",  true),
    };

    private static readonly Color ColGood = new Color(0.3f, 1f, 0.45f);
    private static readonly Color ColBad  = new Color(1f, 0.35f, 0.35f);

    private readonly UpgradeDefinition? _def;
    private readonly AfterAction        _owner;
    private Label         _nameLabel   = null!;
    private VBoxContainer _modList     = null!;
    private Label         _costLabel   = null!;
    private Label         _statusLabel = null!;
    private Button        _buyButton   = null!;
    private bool          _purchased   = false;
    private bool          _revealed    = false;

    public UpgradeCard(UpgradeDefinition? def, AfterAction owner)
    {
        _def   = def;
        _owner = owner;
    }

    public override void _Ready()
    {
        var vbox = new VBoxContainer();
        AddChild(vbox);

        _nameLabel = new Label
        {
            Text = "???",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 15);
        vbox.AddChild(_nameLabel);

        vbox.AddChild(new HSeparator());

        _modList = new VBoxContainer
        {
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        vbox.AddChild(_modList);

        vbox.AddChild(new HSeparator());

        _costLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = Colors.Yellow,
        };
        vbox.AddChild(_costLabel);

        _statusLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = Colors.LimeGreen,
        };
        vbox.AddChild(_statusLabel);

        _buyButton = new Button
        {
            Text = "BUY",
            Disabled = true,
            Visible = _def != null,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        _buyButton.Pressed += OnBuy;
        vbox.AddChild(_buyButton);
    }

    public void Reveal()
    {
        _revealed = true;
        if (_def == null)
        {
            _nameLabel.Text     = "— LOCKED —";
            _nameLabel.Modulate = Colors.DimGray;
            return;
        }

        _nameLabel.Text = _def.Name;

        foreach (Node c in _modList.GetChildren()) c.QueueFree();
        foreach (var m in _def.Modifiers)
        {
            bool found = StatMeta.TryGetValue(m.Stat, out var meta);
            string statLabel = found ? meta.Label : m.Stat;
            bool   posIsGood = found ? meta.PosIsGood : true;

            if (m.Flat != 0f)
            {
                bool   good = (m.Flat > 0f) == posIsGood;
                string val  = m.Flat > 0f
                    ? $"+{m.Flat:0.###}"
                    : $"\u2212{Mathf.Abs(m.Flat):0.###}";   // − (minus sign)
                AddModRow(statLabel, val, good);
            }

            if (m.Multiplier != 1f)
            {
                bool   good = (m.Multiplier > 1f) == posIsGood;
                string val  = $"\u00d7{m.Multiplier:0.###}";  // ×
                AddModRow(statLabel, val, good);
            }
        }

        _costLabel.Text = "Cost: " + string.Join("  ", _def.Cost.Select(kv => $"{kv.Value}\u00d7 {kv.Key}"));
        RefreshAffordability();
    }

    private void AddModRow(string statName, string value, bool good)
    {
        var row = new HBoxContainer();

        var nameLbl = new Label
        {
            Text = statName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        nameLbl.AddThemeFontSizeOverride("font_size", 12);

        var valLbl = new Label
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Right,
            Modulate = good ? ColGood : ColBad,
        };
        valLbl.AddThemeFontSizeOverride("font_size", 12);

        row.AddChild(nameLbl);
        row.AddChild(valLbl);
        _modList.AddChild(row);
    }

    public void RefreshAffordability()
    {
        if (!_revealed || _purchased || _def == null) return;
        bool can = _owner.CanAfford(_def);
        _buyButton.Disabled = !can;
        Modulate = can ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    public void MarkPurchased()
    {
        _purchased          = true;
        _buyButton.Disabled = true;
        _statusLabel.Text   = "ACQUIRED";
        Modulate            = new Color(0.4f, 0.85f, 0.4f, 1f);
    }

    private void OnBuy()
    {
        if (_def != null) _owner.PurchaseUpgrade(_def, this);
    }
}
