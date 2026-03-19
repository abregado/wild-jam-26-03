using Godot;

/// <summary>
/// Controls the day/night cycle. Attach as a child of Main.
///
/// Time is divided into two equal phases of DayNightPhaseDuration seconds each:
///   Phase 0 (0 → PhaseDuration)   : noon → sunset
///   Phase 1 (PhaseDuration → 2×)  : sunset → night
/// After both phases the scene stays locked at night. Reset() restarts from noon.
///
/// Drives:
///   - Sun (DirectionalLight3D): elevation angle, colour, energy
///   - WorldEnvironment.Environment: ambient colour + energy, sky top colour
///
/// Config keys (game_config.json "day_night"):
///   "enabled"        : bool  — set false to freeze at noon
///   "phase_duration" : float — seconds per phase (default 240)
/// </summary>
public partial class DayNightCycle : Node
{
    // ── Sun elevation angles (degrees; negative = above horizon) ────────────
    private const float ElevNoon    = -60f;
    private const float ElevSunset  =  -5f;
    private const float ElevNight   =  25f;   // well below horizon
    private const float SunAzimuth  =  30f;   // fixed horizontal angle (matches original scene)

    // ── Colour keyframes ────────────────────────────────────────────────────
    private static readonly Color SunColorNoon   = new(1.00f, 0.92f, 0.78f);
    private static readonly Color SunColorSunset = new(1.00f, 0.38f, 0.08f);
    private static readonly Color SunColorNight  = new(0.03f, 0.03f, 0.10f);

    private static readonly Color AmbNoon   = new(0.90f, 0.75f, 0.55f);
    private static readonly Color AmbSunset = new(0.65f, 0.30f, 0.08f);
    private static readonly Color AmbNight  = new(0.04f, 0.07f, 0.18f);

    private static readonly Color SkyNoon   = new(0.15f, 0.35f, 0.70f);
    private static readonly Color SkySunset = new(0.10f, 0.04f, 0.12f);
    private static readonly Color SkyNight  = new(0.01f, 0.01f, 0.04f);

    // ── Energy keyframes ────────────────────────────────────────────────────
    private const float SunEnergyNoon   = 1.4f;
    private const float SunEnergySunset = 0.8f;
    private const float SunEnergyNight  = 0.0f;

    private const float AmbEnergyNoon   = 0.30f;
    private const float AmbEnergySunset = 0.45f;
    private const float AmbEnergyNight  = 0.65f;

    // ── State ───────────────────────────────────────────────────────────────
    private DirectionalLight3D _sun = null!;
    private Environment _env = null!;
    private ProceduralSkyMaterial? _skyMat;
    private float _elapsed;
    private float _phaseDuration;
    private bool _enabled;

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _enabled       = config.DayNightEnabled;
        _phaseDuration = config.DayNightPhaseDuration;

        _sun = GetParent().GetNode<DirectionalLight3D>("Sun");
        var we = GetParent().GetNode<WorldEnvironment>("WorldEnvironment");
        _env = we.Environment;
        _skyMat = _env.Sky?.SkyMaterial as ProceduralSkyMaterial;

        ApplyTime(0f);
    }

    /// <summary>Called at the start of each new raid to reset to noon.</summary>
    public void Reset()
    {
        _elapsed = 0f;
        ApplyTime(0f);
    }

    public override void _Process(double delta)
    {
        if (!_enabled) return;

        float total = _phaseDuration * 2f;
        if (_elapsed >= total) return;

        _elapsed = Mathf.Min(_elapsed + (float)delta, total);
        ApplyTime(_elapsed);
    }

    private void ApplyTime(float t)
    {
        float total = _phaseDuration * 2f;
        float tNorm = total > 0f ? Mathf.Clamp(t / total, 0f, 1f) : 0f; // 0=noon, 1=night

        float phase0 = Mathf.Clamp(tNorm * 2f, 0f, 1f);       // 0→1 during first half
        float phase1 = Mathf.Clamp(tNorm * 2f - 1f, 0f, 1f);  // 0→1 during second half

        // Sun elevation: noon → sunset → night
        float elev = tNorm < 0.5f
            ? Mathf.Lerp(ElevNoon,   ElevSunset, phase0)
            : Mathf.Lerp(ElevSunset, ElevNight,  phase1);

        _sun.RotationDegrees = new Vector3(elev, SunAzimuth, 0f);

        // Sun colour and energy
        Color sunCol = tNorm < 0.5f
            ? SunColorNoon.Lerp(SunColorSunset, phase0)
            : SunColorSunset.Lerp(SunColorNight, phase1);
        _sun.LightColor  = sunCol;
        _sun.LightEnergy = tNorm < 0.5f
            ? Mathf.Lerp(SunEnergyNoon,   SunEnergySunset, phase0)
            : Mathf.Lerp(SunEnergySunset, SunEnergyNight,  phase1);

        // Ambient
        Color ambCol = tNorm < 0.5f
            ? AmbNoon.Lerp(AmbSunset, phase0)
            : AmbSunset.Lerp(AmbNight, phase1);
        _env.AmbientLightColor  = ambCol;
        _env.AmbientLightEnergy = tNorm < 0.5f
            ? Mathf.Lerp(AmbEnergyNoon,   AmbEnergySunset, phase0)
            : Mathf.Lerp(AmbEnergySunset, AmbEnergyNight,  phase1);

        // Sky top colour
        if (_skyMat != null)
        {
            Color skyCol = tNorm < 0.5f
                ? SkyNoon.Lerp(SkySunset, phase0)
                : SkySunset.Lerp(SkyNight, phase1);
            _skyMat.SkyTopColor = skyCol;
        }
    }
}
