using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

public static class ForestMapUi
{
    private static readonly Color Ink = new("dce5e2");
    private const string InspectionMeta = "forest_inspection";

    public static Control? DefaultFocus { get; private set; }

    // A completed room can remain the native CurrentRoom while exploration is
    // already open. Refreshes must preserve the mode chosen by OpenMap.
    public static bool IsInspection(NMapScreen screen) =>
        screen.GetNodeOrNull<Control>("BlackFlowSpireMap") is not { } root
        || !root.HasMeta(InspectionMeta) || root.GetMeta(InspectionMeta).AsBool();

    public static void Show(NMapScreen screen, RunState state, bool inspection, int? restoreSelection = null,
        bool focusCurrent = false)
    {
        DefaultFocus = null;
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        var engine = ForestGenerationService.GetEngine(state);
        var canLead = !inspection && !ForestTeam.IsGuest;
        var cameraKey = map.Seed + "|" + map.ActIndex;
        var cameraState = new Godot.Collections.Dictionary();
        if (screen.GetNodeOrNull<Control>("BlackFlowSpireMap") is { } old)
        {
            if (old.HasMeta("forest_camera_map") && old.GetMeta("forest_camera_map").AsString() == cameraKey)
                cameraState = old.GetNode<Control>("ExplorationField").Call("capture_camera").AsGodotDictionary();
            screen.RemoveChild(old);
            old.QueueFree();
        }
        var root = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapScreen.tscn").Instantiate<Control>();
        screen.AddChild(root);
        root.SetMeta("forest_camera_map", cameraKey);
        root.SetMeta(InspectionMeta, inspection);
        root.GetNode<TextureRect>("Background").Texture = ForestMapAssets.Background(map.ActIndex);

        var field = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapViewport.tscn").Instantiate<Control>();
        root.AddChild(field);
        var spacing = root.Get("grid_spacing").AsVector2();
        var worldSize = new Vector2((map.Options.Width - 1) * spacing.X + 160, (map.Options.Height - 1) * spacing.Y + 160);
        var world = field.GetNode<Control>("RoadsAndRooms");
        world.Size = worldSize;

        Vector2 Position(ForestNode node) => new(80 + node.X * spacing.X,
            80 + (map.Options.Height - 1 - node.Y) * spacing.Y);
        var routes = ForestTravelRules.FindRoutes(map, engine);
        var roadScene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapRoad.tscn");
        var roads = new List<(Control Visual, int From, int To)>();
        foreach (var node in map.Nodes)
        foreach (var id in node.Neighbors.Where(id => id > node.Id))
        {
            var other = map.GetNode(id);
            var start = Position(node);
            var distance = start.DistanceTo(Position(other));
            var road = roadScene.Instantiate<Control>();
            road.Size = new Vector2(46, Math.Max(1, distance - 56));
            road.Position = (start + Position(other)) / 2 - road.Size / 2;
            road.PivotOffset = road.Size / 2;
            road.Rotation = (Position(other) - start).Angle() - Mathf.Pi / 2;
            world.AddChild(road);
            var passable = engine.LoadedPart == null && !engine.IsOverloaded
                && ForestRoadPresentation.IsPassable(map, progress, node, other, routes, ForestRuntime.IsTraveling);
            road.Call("configure", passable, 1 + (node.Id * 17 + id * 31) % 7, (node.Id * 43 + id * 17) % 97 / 29f);
            roads.Add((road, node.Id, id));
        }

        var buttons = new Dictionary<int, Button>();
        var reachability = new Dictionary<int, (Control Ring, Control? Caption)>();
        var captionScene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapNodeCaption.tscn");
        var selectionMarker = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapSelection.tscn").Instantiate<Control>();
        world.AddChild(selectionMarker);
        var bossTitle = state.Act.BossEncounter.Title.GetFormattedText();
        ForestNodeDetailsPanel? details = null;
        int? selectedNodeId = null;

        void PreviewRoute(ForestTravelRoute? route)
        {
            var edges = new HashSet<(int, int)>();
            if (route != null)
                for (var index = 1; index < route.NodeIds.Count; index++)
                {
                    var from = route.NodeIds[index - 1];
                    var to = route.NodeIds[index];
                    edges.Add((Math.Min(from, to), Math.Max(from, to)));
                }
            foreach (var road in roads)
            {
                var selected = edges.Contains((road.From, road.To));
                road.Visual.Call("set_selected", selected);
            }
        }

        void SelectNode(int id)
        {
            if (ForestRuntime.IsTraveling || !inspection && progress.HuntPhase != ForestHuntPhase.None) return;
            selectedNodeId = id;
            var info = ForestNodeInfoPresentation.Get(map, progress, id, !canLead, ForestRuntime.IsTraveling, bossTitle, engine);
            selectionMarker.Call("select_node", Position(map.GetNode(id)), info.View.IconKey == ForestNodeVisuals.EMPTY);
            PreviewRoute(info.CanMove ? info.Route : null);
            details!.Show(info);
            field.Call("ensure_node_visible", Position(map.GetNode(id)));
            buttons[id].FocusNext = (info.CanMove ? details.MoveButton : info.CanViewEnemyIntel ? details.EnemyIntelButton : buttons[id]).GetPath();
            details.MoveButton.FocusPrevious = buttons[id].GetPath();
            details.MoveButton.FocusNeighborLeft = buttons[id].GetPath();
            details.EnemyIntelButton.FocusNext = (info.CanMove ? details.MoveButton : buttons[id]).GetPath();
        }

        void ClearSelection()
        {
            selectedNodeId = null;
            selectionMarker.Call("clear_selection");
            PreviewRoute(null);
            details!.Root.Hide();
        }

        bool IsReachable(ForestNode node, IReadOnlyDictionary<int, ForestTravelRoute> availableRoutes) =>
            canLead && !ForestRuntime.IsTraveling && !progress.PendingNodeId.HasValue
            && progress.HuntPhase == ForestHuntPhase.None
            && availableRoutes.TryGetValue(node.Id, out var route)
            && progress.ActionPointsRemaining >= route.ActionPointCost && ForestResidentRules.CanEnter(map, node);

        void PreviewMovement(ForestProcessedPart? part)
        {
            var previewRoutes = ForestTravelRules.FindRoutes(map, part, engine.IsOverloaded);
            foreach (var (id, (ring, caption)) in reachability)
            {
                ring.Visible = IsReachable(map.GetNode(id), previewRoutes);
                caption?.Call("set_reachable", ring.Visible || id == map.PositionNodeId);
            }
            foreach (var (visual, from, to) in roads)
                visual.Call("set_passable", part == null && !engine.IsOverloaded && ForestRoadPresentation.IsPassable(
                    map, progress, map.GetNode(from), map.GetNode(to), previewRoutes, ForestRuntime.IsTraveling));
        }

        void MoveToSelection()
        {
            if (!selectedNodeId.HasValue || !ForestTeam.CanTravel(state)) return;
            // Recheck the current rules at confirmation; selection itself never travels.
            var info = ForestNodeInfoPresentation.Get(map, progress, selectedNodeId.Value, !canLead, ForestRuntime.IsTraveling, bossTitle, engine);
            details!.Show(info);
            if (!info.CanMove) return;
            details.MoveButton.Disabled = true;
            field.Call("set_interaction_enabled", false);
            foreach (var button in buttons.Values) button.Disabled = true;
            TaskHelper.RunSafely(ForestRuntime.Move(selectedNodeId.Value));
        }

        foreach (var node in map.Nodes)
        {
            var view = ForestNodePresentation.Get(node, bossTitle: bossTitle);
            var occupied = ForestResidentRules.Occupant(map, node.Id) != null;
            var isCurrent = node.Id == map.PositionNodeId;
            var enabled = IsReachable(node, routes);
            var button = new Button
            {
                Name = "Node" + node.Id, Position = Position(node) - new Vector2(80, 40), Size = new Vector2(160, 124),
                Disabled = ForestRuntime.IsTraveling
            };
            ForestMapAssets.ClearButtonStyle(button);
            world.AddChild(button);
            buttons[node.Id] = button;
            var visual = new Control
            {
                Position = new Vector2(40, 0), Size = new Vector2(80, 80), PivotOffset = new Vector2(40, 40),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            button.AddChild(visual);
            var ring = ForestMapAssets.Focus(view.IconKey == ForestNodeVisuals.EMPTY ? 58 : 104,
                new Color(0.83f, 0.92f, 0.9f, 0.26f));
            ring.Visible = enabled && !isCurrent;
            visual.AddChild(ring);
            visual.AddChild(Glyph(view.IconKey, 80, screen, state));
            if (occupied)
            {
                var marker = ForestMapAssets.NodeImage(ForestNodeVisuals.BATTLE_SAVAGE_BUOY, 54, details: true);
                marker.Position = new Vector2(43, -15);
                visual.AddChild(marker);
            }
            var captionText = ForestNodePresentation.CaptionForIcon(view.IconKey);
            Control? caption = null;
            if (!string.IsNullOrWhiteSpace(captionText))
            {
                caption = captionScene.Instantiate<Control>();
                visual.AddChild(caption);
                caption.GetNode<Label>("Row/Name").AddThemeFontOverride("font", ForestMapAssets.Font);
                caption.Call("bind_caption", captionText, node.Completed && !occupied, enabled || isCurrent);
            }
            reachability.Add(node.Id, (ring, caption));
            void Focus(bool focused)
            {
                visual.Scale = Vector2.One * (focused ? 1.05f : 1);
            }
            button.MouseEntered += () => Focus(true);
            button.MouseExited += () => Focus(false);
            button.FocusEntered += () =>
            {
                Focus(true);
                field.Call("ensure_keyboard_focus", Position(node));
            };
            button.FocusExited += () => Focus(false);
            button.GuiInput += input => field.Call("map_gui_input", input, button);
            var target = node.Id;
            button.Pressed += () => SelectNode(target);
            if (enabled && DefaultFocus == null) DefaultFocus = button;
        }
        // The location marker is independent of selection and is rebuilt only
        // from the saved current position, never from the node being inspected.
        // Use sibling order so both markers stay below detail/modal UI layers.
        world.MoveChild(selectionMarker, world.GetChildCount() - 1);
        var currentMarker = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/ForestMapCurrentMarker.tscn").Instantiate<Control>();
        currentMarker.Position = Position(map.GetNode(map.PositionNodeId));
        world.AddChild(currentMarker);
        foreach (var node in map.Nodes)
        {
            var button = buttons[node.Id];
            foreach (var (dx, dy, direction) in new[] { (-1, 0, Side.Left), (1, 0, Side.Right), (0, 1, Side.Top), (0, -1, Side.Bottom) })
            {
                var neighbor = map.Nodes.FirstOrDefault(candidate => candidate.X == node.X + dx && candidate.Y == node.Y + dy && !buttons[candidate.Id].Disabled);
                var path = (neighbor == null ? button : buttons[neighbor.Id]).GetPath();
                switch (direction)
                {
                    case Side.Left: button.FocusNeighborLeft = path; break;
                    case Side.Right: button.FocusNeighborRight = path; break;
                    case Side.Top: button.FocusNeighborTop = path; break;
                    case Side.Bottom: button.FocusNeighborBottom = path; break;
                }
            }
        }

        details = new ForestNodeDetailsPanel(root, (key, size) => Glyph(key, size, screen, state, details: true), MoveToSelection, () =>
        {
            if (!selectedNodeId.HasValue) return;
            var node = map.GetNode(selectedNodeId.Value);
            var setup = ForestResidentRules.CombatSetupFor(map, node);
            if (ForestNodePresentation.Get(node, map: map).IsMasked || setup == null) return;
            ForestEnemyIntelModal.Open(setup);
        });
        field.Connect("background_clicked", Callable.From(ClearSelection));
        if (inspection || canLead && progress.ActionPointsRemaining < map.Options.MoveCost && progress.HuntPhase == ForestHuntPhase.None)
        {
            var action = new Button { Text = ForestText.Get(inspection ? "UI.ACTION.return" : "UI.ACTION.end") };
            ForestMapAssets.ClearButtonStyle(action);
            action.AddThemeFontOverride("font", ForestMapAssets.Font);
            action.AddThemeFontSizeOverride("font_size", 22);
            action.AddThemeColorOverride("font_color", Ink);
            action.AddThemeColorOverride("font_hover_color", new Color("b9f4ec"));
            root.AddChild(action);
            action.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
            action.OffsetLeft = -295;
            action.OffsetRight = -45;
            action.OffsetTop = -64;
            action.OffsetBottom = -16;
            action.Pressed += () =>
            {
                if (inspection) ForestRuntime.CloseMap(screen);
                else RunManager.Instance.Abandon();
            };
            DefaultFocus ??= action;
        }
        DefaultFocus ??= buttons[map.PositionNodeId];
        field.Call("configure_camera", worldSize, cameraState);
        if (focusCurrent) field.CallDeferred("ensure_node_visible", Position(map.GetNode(map.PositionNodeId)));
        field.Call("set_interaction_enabled", !ForestRuntime.IsTraveling && progress.HuntPhase == ForestHuntPhase.None);
        if (restoreSelection.HasValue && buttons.ContainsKey(restoreSelection.Value)) SelectNode(restoreSelection.Value);
        if (canLead && progress.HuntPhase != ForestHuntPhase.None)
        {
            DefaultFocus = null;
            ForestHuntUi.Show(root, screen, state);
        }
        else if (canLead && ForestPassageRules.IsPending(map, progress))
        {
            DefaultFocus = null;
            field.Call("set_interaction_enabled", false);
            ForestPassageUi.Show(root, screen, state);
        }
        else if (canLead && ForestFloorExitRules.IsPending(map, progress))
        {
            DefaultFocus = null;
            field.Call("set_interaction_enabled", false);
            root.AddChild(new ForestFloorExitUi(state, screen) { Name = "ForestFloorExit" });
        }
        ForestPartsUi.Attach(root, state, screen, inspection, ClearSelection, PreviewMovement);
        if (ForestTeam.IsOnline && !inspection) root.AddChild(new ForestTeamMapStatus(state));
        ForestLocalization.RefreshWithLocale(root, () =>
        {
            if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree() || ForestRuntime.IsTraveling) return;
            Show(screen, state, inspection, selectedNodeId);
            ActiveScreenContext.Instance.Update();
        });
        root.Call("refresh_layout");
    }

    private static Control Glyph(string key, float size, NMapScreen screen, RunState state, bool details = false)
    {
        var root = new Control { Size = Vector2.One * size, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (key == ForestNodeVisuals.BATTLE_BOSS)
        {
            // Keep native Spine/static boss rendering, but disable every native input target.
            // Movement is handled exclusively by the enclosing Forest button.
            var boss = NBossMapPoint.Create(state.Map.BossMapPoint, screen, state);
            root.AddChild(boss);
            boss.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            boss.Size = new Vector2(374, 306);
            boss.PivotOffset = Vector2.Zero;
            boss.Scale = Vector2.One * (size / 370);
            boss.Position = (root.Size - boss.Size * boss.Scale) / 2;
            root.Ready += () =>
            {
                boss.Disable();
                boss.State = MapPointState.Traveled;
                boss.MouseFilter = Control.MouseFilterEnum.Ignore;
                boss.FocusMode = Control.FocusModeEnum.None;
                foreach (var control in boss.FindChildren("*", "Control", true, false).OfType<Control>())
                {
                    control.MouseFilter = Control.MouseFilterEnum.Ignore;
                    control.FocusMode = Control.FocusModeEnum.None;
                }
            };
            return root;
        }
        var icon = ForestMapAssets.NodeImage(key, size, details);
        root.AddChild(icon);
        return root;
    }
}
