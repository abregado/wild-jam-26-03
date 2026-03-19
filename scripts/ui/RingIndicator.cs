using Godot;

/// <summary>
/// Screen-space bouncing ring drawn around a 3D world target.
/// Add to a CanvasLayer. Set Target and Cam each time you want to highlight something.
/// Hides itself automatically when Target is null or behind the camera.
/// </summary>
public partial class RingIndicator : Control
{
    public Node3D?   Target      { get; set; }
    public Camera3D? Cam         { get; set; }
    public float     WorldRadius { get; set; } = 1.5f;
    public float     LineWidth   { get; set; } = 3f;
    public Color     RingColor   { get; set; } = new Color(1f, 0.88f, 0.2f, 0.92f);

    private float _time;
    private float _radius = 55f;

    public override void _Process(double delta)
    {
        if (Target == null || Cam == null || !IsInstanceValid(Target) || !Target.IsInsideTree())
        {
            Visible = false;
            return;
        }

        if (Cam.IsPositionBehind(Target.GlobalPosition))
        {
            Visible = false;
            return;
        }

        Visible = true;
        Position = Cam.UnprojectPosition(Target.GlobalPosition);

        // Scale ring radius to match the world-space size of the target.
        float dist     = (Target.GlobalPosition - Cam.GlobalPosition).Length();
        Vector2 vSize  = GetViewport().GetVisibleRect().Size;
        float focalLen = vSize.X / (2f * Mathf.Tan(Mathf.DegToRad(Cam.Fov * 0.5f)));
        _radius = Mathf.Clamp((WorldRadius / Mathf.Max(dist, 0.01f)) * focalLen + 12f, 20f, 350f);

        _time += (float)delta;
        float bounce = 1f + 0.14f * Mathf.Sin(_time * Mathf.Tau * 1.3f);
        Scale = new Vector2(bounce, bounce);

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 64, RingColor, LineWidth, true);
    }
}
