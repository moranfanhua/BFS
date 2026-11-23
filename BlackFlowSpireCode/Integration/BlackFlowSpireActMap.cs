using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BlackFlowSpire.Integration;

// Native map data adapter. BlackFlowSpireRuntime handles traversal and fixed room selection.
public sealed class BlackFlowSpireActMap : ActMap
{
    private readonly Dictionary<MapCoord, BlackFlowSpireNode> _nodeData = [];
    private readonly string _seed;
    private readonly int _act;
    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    protected override MapPoint?[,] Grid { get; }

    public BlackFlowSpireActMap(BlackFlowSpireMap snapshot)
    {
        _seed = snapshot.Seed;
        _act = snapshot.ActIndex;
        BlackFlowSpireMapValidator.Validate(snapshot);
        Grid = new MapPoint?[snapshot.Options.Width, snapshot.Options.Height];
        var points = new Dictionary<int, MapPoint>();
        foreach (var node in snapshot.Nodes)
        {
            var point = new MapPoint(node.X, node.Y)
            {
                PointType = PointTypeFor(node),
                CanBeModified = false
            };
            points.Add(node.Id, point);
            _nodeData.Add(point.coord, node);
            // Native saved maps keep start/Boss outside Grid. Duplicating them breaks identity after load.
            if (node.Id != snapshot.StartNodeId && node.Id != snapshot.BossNodeId)
                Grid[node.X, node.Y] = point;
        }
        StartingMapPoint = points[snapshot.StartNodeId];
        BossMapPoint = points[snapshot.BossNodeId];
        startMapPoints.Add(StartingMapPoint);
        foreach (var node in snapshot.Nodes)
            foreach (var neighbor in node.Neighbors)
                points[node.Id].AddChildPoint(points[neighbor]);
    }

    public BlackFlowSpireNode GetNodeData(MapCoord coordinate) => _nodeData[coordinate];

    internal bool Rebind(BlackFlowSpireMap snapshot)
    {
        if (_act != snapshot.ActIndex || _seed != snapshot.Seed || snapshot.Nodes.Count != _nodeData.Count) return false;
        foreach (var node in snapshot.Nodes)
            _nodeData[new MapCoord(node.X, node.Y)] = node;
        RefreshPointTypes();
        return true;
    }

    // Native map points are cached separately from forest content. Keep their saved
    // types in sync when a one-time node turns empty, without rewriting floor history.
    public void RefreshPointTypes()
    {
        foreach (var (coordinate, node) in _nodeData)
            GetPoint(coordinate)!.PointType = PointTypeFor(node);
    }

    private static MapPointType PointTypeFor(BlackFlowSpireNode node) => node.Content switch
    {
        BlackFlowSpireNodeContent.Empty => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.NormalCombat => MapPointType.Monster,
        BlackFlowSpireNodeContent.EliteCombat => MapPointType.Elite,
        BlackFlowSpireNodeContent.Event => MapPointType.Unknown,
        BlackFlowSpireNodeContent.Reward => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.Shop => MapPointType.Shop,
        BlackFlowSpireNodeContent.Door => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.BattleSavage => MapPointType.Monster,
        BlackFlowSpireNodeContent.Light => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.Boss => MapPointType.Boss,
        BlackFlowSpireNodeContent.Final => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.Evacuate => MapPointType.Unassigned,
        BlackFlowSpireNodeContent.Rest or BlackFlowSpireNodeContent.Sacrifice or BlackFlowSpireNodeContent.Expedition => MapPointType.Unassigned,
        _ => throw new InvalidDataException("Unknown node content.")
    };

    // null means a map-only node (empty or a custom map interaction), never a randomized room.
    public RoomType? GetFixedRoomType(MapCoord coordinate) => GetNodeData(coordinate).Content switch
    {
        BlackFlowSpireNodeContent.Empty => null,
        BlackFlowSpireNodeContent.NormalCombat => RoomType.Monster,
        BlackFlowSpireNodeContent.EliteCombat => RoomType.Elite,
        BlackFlowSpireNodeContent.Event => RoomType.Event,
        BlackFlowSpireNodeContent.Reward => null,
        BlackFlowSpireNodeContent.Shop => RoomType.Shop,
        BlackFlowSpireNodeContent.Door => null,
        BlackFlowSpireNodeContent.BattleSavage => RoomType.Monster,
        BlackFlowSpireNodeContent.Light => null,
        BlackFlowSpireNodeContent.Boss => RoomType.Boss,
        BlackFlowSpireNodeContent.Final => null,
        BlackFlowSpireNodeContent.Evacuate => null,
        BlackFlowSpireNodeContent.Rest or BlackFlowSpireNodeContent.Sacrifice or BlackFlowSpireNodeContent.Expedition => null,
        _ => throw new InvalidDataException("Unknown node content.")
    };
}
