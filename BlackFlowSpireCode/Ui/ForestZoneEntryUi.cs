using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal partial class ForestZoneEntryUi : CanvasLayer
{
    private Control _art = null!;
    private Control _frame = null!;
    private ColorRect _backdrop = null!;
    private ColorRect _veil = null!;
    private AnimationPlayer _player = null!;
    private Tween? _reveal;
    private Viewport? _viewport;
    private bool _closing;

    public static void TryShow(int actIndex, bool playTransitionSound = false)
    {
        if (NRun.Instance is not { } run || !run.IsInsideTree()) return;
        ForestZoneEntryUi? overlay = null;
        try
        {
            var floor = actIndex + 1;
            var scenePath = $"res://BlackFlowSpire/Scenes/Map/ZoneEntry/ZoneEntry{floor:00}.tscn";
            var scene = ResourceLoader.Load<PackedScene>(scenePath)
                ?? throw new InvalidOperationException("Zone entry scene unavailable: " + scenePath);
            overlay = new ForestZoneEntryUi
            {
                Name = "ForestZoneEntry", Layer = 50, ProcessMode = ProcessModeEnum.Always,
                _art = scene.Instantiate<Control>()
            };
            run.GetNodeOrNull<ForestZoneEntryUi>("ForestZoneEntry")?.Free();
            overlay.ShowOver(run, floor);
            if (playTransitionSound) overlay.PlayTransitionSound();
            Entry.Logger.Info($"[Forest] Zone entry: floor={floor}");
        }
        catch (Exception error)
        {
            overlay?.Free();
            Entry.Logger.Error($"[Forest] Zone entry unavailable for floor {actIndex + 1}: {error}");
        }
    }

    private void PlayTransitionSound()
    {
        var stream = ResourceLoader.Load<AudioStream>("res://BlackFlowSpire/Audio/g_ui_crostage_rl6.wav");
        if (stream == null) return;
        var settings = SaveManager.Instance.SettingsSave;
        var player = new AudioStreamPlayer { Stream = stream, VolumeLinear = settings.VolumeMaster * settings.VolumeSfx };
        AddChild(player);
        player.Play();
    }

    private void BindText(int floor)
    {
        var title = _art.GetNode<Label>("panel_content/text_name/text_name/Graphic");
        var description = _art.GetNode<Label>("panel_content/text_desc/Graphic");
        title.Text = ForestText.Get($"UI.ZONE_ENTRY.{floor:00}.title");
        description.Text = ForestText.Get($"UI.ZONE_ENTRY.{floor:00}.description");
    }

    private void ShowOver(NRun run, int floor)
    {
        var surface = new Control { MouseFilter = Control.MouseFilterEnum.Stop, ClipContents = true };
        _frame = new Control { Size = _art.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        _backdrop = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        _veil = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(surface);
        surface.AddChild(_backdrop);
        surface.AddChild(_frame);
        _frame.AddChild(_art);
        surface.AddChild(_veil);
        BindText(floor);
        run.AddChild(this);
        foreach (var control in new Control[] { surface, _backdrop, _veil })
            control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _viewport = GetViewport();
        _viewport.SizeChanged += Resize;
        Resize();

        _player = _art.GetNode<AnimationPlayer>("AnimationPlayer");
        _player.AnimationFinished += _ => BeginExit();
        _player.Play("enter");
        _player.Advance(0);
        _reveal = CreateTween().SetIgnoreTimeScale();
        _reveal.TweenProperty(_veil, "color:a", 0f, 0.33);
    }

    private void Resize()
    {
        var area = _viewport!.GetVisibleRect().Size;
        var scale = Math.Min(area.X / _frame.Size.X, area.Y / _frame.Size.Y);
        _frame.Scale = Vector2.One * scale;
        _frame.Position = (area - _frame.Size * scale) / 2;
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }
            || input is InputEventScreenTouch { Pressed: false }
            || input.IsActionReleased(MegaInput.confirm) || input.IsActionReleased(MegaInput.select)
            || input.IsActionReleased(MegaInput.cancel) || input.IsActionReleased(MegaInput.back))
            BeginExit();
        GetViewport().SetInputAsHandled();
    }

    private void BeginExit()
    {
        if (_closing) return;
        _closing = true;
        _reveal?.Kill();
        _player.Pause();
        var exit = CreateTween().SetIgnoreTimeScale();
        exit.TweenProperty(_veil, "color:a", 1f, 0.18);
        exit.TweenCallback(Callable.From(() => { _backdrop.Hide(); _art.Hide(); }));
        exit.TweenProperty(_veil, "color:a", 0f, 0.19);
        exit.TweenCallback(Callable.From(QueueFree));
    }

    public override void _ExitTree()
    {
        if (_viewport != null) _viewport.SizeChanged -= Resize;
    }
}
