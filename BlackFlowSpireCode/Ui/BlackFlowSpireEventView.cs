using Godot;

namespace BlackFlowSpire.Ui;

internal readonly record struct BlackFlowSpireEventOption(string Title, string Description, Texture2D Icon);

internal sealed class BlackFlowSpireEventView
{
    private const string ScenePath = "res://BlackFlowSpire/Scenes/Events/BlackFlowSpireEventChoice.tscn";
    private readonly Control _view;

    public BlackFlowSpireEventView(Control parent, Action<int> confirm, Action? leave = null)
    {
        _view = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Control>();
        parent.AddChild(_view);
        _view.Connect("choice_confirmed", Callable.From(confirm));
        if (leave != null) _view.Connect("leave_pressed", Callable.From(leave));
    }

    public Button? GetChoice(int index) => _view.Call("get_choice", index).As<Button>();
    public Button LeaveButton => _view.GetNode<Button>("Canvas/Leave");

    public void SetStory(Texture2D background, string title, string description) =>
        _view.Call("bind_story", background, title, description);

    public void SetOptions(IReadOnlyList<BlackFlowSpireEventOption> options)
    {
        _view.Call("configure_choices", options.Count);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            _view.Call("bind_choice", index, option.Title, option.Description, option.Icon);
        }
    }

    public void ShowResult(string leaveTooltip) => _view.Call("show_result", leaveTooltip);
    public void SetBusy(bool busy) => _view.Call("set_busy", busy);

    public static Texture2D LoadTexture(string name) =>
        ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Events/Exit/{name}.png");
}
