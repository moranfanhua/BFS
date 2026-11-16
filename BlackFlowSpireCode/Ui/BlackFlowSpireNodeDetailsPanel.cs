using BlackFlowSpire.Map.Presentation;
using Godot;

namespace BlackFlowSpire.Ui;

// Layout, textures and typography belong to BlackFlowSpireNodeDetails.tscn.
// This adapter only binds live data and behavior to the named scene controls.
internal sealed class BlackFlowSpireNodeDetailsPanel
{
    private readonly Func<string, float, Control> _glyph;
    private readonly Control _icon;
    private readonly Label _title;
    private readonly Label _type;
    private readonly Label _status;
    private readonly Label _description;
    private readonly Label _rules;
    private readonly Label _actionPoints;
    private readonly Label _cost;
    private readonly Control _moveCostGroup;
    private readonly TextureRect _walkText;
    private readonly TextureRect _unreachableText;
    private readonly TextureRect _driveText;
    private readonly TextureRect _driveUnreachableText;
    private readonly Node2D _moveEffects;
    private readonly Sprite2D _moveCore;
    private readonly Node2D _huntEffects;
    private readonly ShaderMaterial[] _effectMaterials;
    private readonly TextureRect _enemyImage;

    public Control Root { get; }
    public Button MoveButton { get; }
    public Button EnemyIntelButton { get; }

    public BlackFlowSpireNodeDetailsPanel(Control parent, Func<string, float, Control> glyph, Action move, Action enemyIntel)
    {
        _glyph = glyph;
        Root = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/BlackFlowSpireNodeDetails.tscn").Instantiate<Control>();
        parent.AddChild(Root);
        BlackFlowSpireLocalization.BindStaticText(Root);
        BlackFlowSpireHuntUi.Bind(Root.GetNode<Control>("Canvas"));
        Root.Hide();
        _icon = Root.GetNode<Control>("%NodeIcon");
        _icon.GetNode<Control>("EditorPreview").Hide();
        _title = Root.GetNode<Label>("%NodeTitle");
        _type = Root.GetNode<Label>("%NodeType");
        _status = Root.GetNode<Label>("%NodeStatus");
        _description = Root.GetNode<Label>("%NodeDescription");
        _rules = Root.GetNode<Label>("%NodeRules");
        _actionPoints = Root.GetNode<Control>("%ActionPointsWidget").GetNode<Label>("%ActionPointsValue");
        MoveButton = Root.GetNode<Button>("%MoveToSelectedNode");
        _cost = MoveButton.GetNode<Label>("Cost/MoveCost");
        _moveCostGroup = MoveButton.GetNode<Control>("Cost");
        _walkText = MoveButton.GetNode<TextureRect>("WalkText");
        _unreachableText = MoveButton.GetNode<TextureRect>("UnreachableText");
        _driveText = MoveButton.GetNode<TextureRect>("DriveText");
        _driveUnreachableText = MoveButton.GetNode<TextureRect>("DriveUnreachableText");
        Root.GetNode<Control>("%ActionPointsWidget").Hide();
        _moveEffects = MoveButton.GetNode<Node2D>("Effects");
        _moveCore = _moveEffects.GetNode<Sprite2D>("Core");
        _huntEffects = MoveButton.GetNode<Node2D>("HuntEffects");
        _effectMaterials = _moveEffects.GetChildren().Concat(_huntEffects.GetChildren()).OfType<CanvasItem>()
            .Select(item => item.Material).OfType<ShaderMaterial>().ToArray();
        EnemyIntelButton = Root.GetNode<Button>("%EnemyIntelligence");
        _enemyImage = EnemyIntelButton.GetNode<TextureRect>("Image");
        EnemyIntelButton.Pressed += enemyIntel;
        MoveButton.Pressed += move;
        MoveButton.MouseEntered += () => { if (!MoveButton.Disabled) SetEffectTint(new Color(1.15f, 1.15f, 1.15f)); };
        MoveButton.MouseExited += () => SetEffectTint(MoveButton.Disabled ? new Color(0.45f, 0.45f, 0.45f, 0.65f) : Colors.White);
    }

    private void SetEffectTint(Color color) => _moveEffects.Modulate = _huntEffects.Modulate = color;

    public void Show(BlackFlowSpireNodeInfo info)
    {
        foreach (var child in _icon.GetChildren().Where(child => child.Name != "EditorPreview"))
        {
            _icon.RemoveChild(child);
            child.QueueFree();
        }
        var size = Math.Min(_icon.Size.X, _icon.Size.Y);
        var glyph = _glyph(info.View.IconKey, size);
        glyph.Position = (_icon.Size - glyph.Size) / 2;
        _icon.AddChild(glyph);
        _actionPoints.Text = info.RemainingActionPoints.ToString();
        _title.Text = info.View.Title;
        _title.Visible = !string.IsNullOrWhiteSpace(info.View.Title);
        _type.Text = info.View.Type;
        _type.Visible = !string.IsNullOrWhiteSpace(info.View.Type);
        _status.Text = info.Status;
        _description.Text = info.Description;
        _description.Visible = !string.IsNullOrWhiteSpace(info.Description);
        _rules.Text = info.RulesText;
        _rules.Visible = !string.IsNullOrWhiteSpace(info.RulesText);
        var visual = BlackFlowSpireNodeVisuals.Get(info.View.IconKey);
        Root.Call("set_visual_theme", visual.Theme, _description.Visible);
        // Drive and hunted variants keep their own native core. Only walking
        // changes with the target's visible theme; never inspect hidden content.
        var usingPart = info.MovementName != null;
        var core = usingPart ? "purple" : info.View.IsMasked ? "grey" : visual.WalkCoreTexture;
        _moveCore.Texture = ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Map/Movement/{core}.png");
        EnemyIntelButton.Visible = info.HasEnemyIntel;
        EnemyIntelButton.Disabled = !info.CanViewEnemyIntel;
        _enemyImage.Modulate = info.CanViewEnemyIntel ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
        MoveButton.Visible = !info.IsCurrent;
        MoveButton.Disabled = !info.CanMove;
        _walkText.Visible = info.CanMove && !usingPart;
        _unreachableText.Visible = !info.CanMove && !usingPart;
        _driveText.Visible = info.CanMove && usingPart;
        _driveUnreachableText.Visible = !info.CanMove && usingPart;
        _moveCostGroup.Visible = info.CanMove;
        _moveEffects.Visible = !info.WarnsOfHunt;
        _huntEffects.Visible = info.WarnsOfHunt;
        SetEffectTint(info.CanMove ? Colors.White : new Color(0.45f, 0.45f, 0.45f, 0.65f));
        foreach (var material in _effectMaterials) material.SetShaderParameter("animate", info.CanMove);
        _cost.Text = info.CanMove ? BlackFlowSpireText.Get("UI.COST.amount", ("Amount", info.Route!.ActionPointCost)) : "";
        Root.Show();
    }
}
