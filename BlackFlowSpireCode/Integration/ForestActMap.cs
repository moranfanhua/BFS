using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace BlackFlowSpire.Integration;

// Native map data adapter. ForestRuntime handles traversal and fixed room selection.
public sealed class ForestActMap : ActMap
{
    private readonly Dictionary<MapCoord, ForestNode> _nodeData = [];
    public override MapPoint BossMapPoint { get; }
    public override MapPoint StartingMapPoint { get; }
    protected override MapPoint?[,] Grid { get; }

    public ForestActMap(ForestMap snapshot)
    {
        ForestMapValidator.Validate(snapshot);
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

    public ForestNode GetNodeData(MapCoord coordinate) => _nodeData[coordinate];

    // Native map points are cached separately from forest content. Keep their saved
    // types in sync when a one-time node turns empty, without rewriting floor history.
    public void RefreshPointTypes()
    {
        foreach (var (coordinate, node) in _nodeData)
            GetPoint(coordinate)!.PointType = PointTypeFor(node);
    }

    private static MapPointType PointTypeFor(ForestNode node) => node.Content switch
    {
        ForestNodeContent.Empty => MapPointType.Unassigned,
        ForestNodeContent.NormalCombat => MapPointType.Monster,
        ForestNodeContent.EliteCombat => MapPointType.Elite,
        ForestNodeContent.Event => MapPointType.Unknown,
        ForestNodeContent.Reward => MapPointType.Treasure,
        ForestNodeContent.Shop => MapPointType.Shop,
        ForestNodeContent.Door => MapPointType.Unassigned,
        ForestNodeContent.BattleSavage => MapPointType.Monster,
        ForestNodeContent.Light => MapPointType.Unassigned,
        ForestNodeContent.Boss => MapPointType.Boss,
        ForestNodeContent.Final => MapPointType.Unassigned,
        ForestNodeContent.Evacuate => MapPointType.Unassigned,
        _ => throw new InvalidDataException("Unknown node content.")
    };

    // null means a map-only node (empty or a custom map interaction), never a randomized room.
    public RoomType? GetFixedRoomType(MapCoord coordinate) => GetNodeData(coordinate).Content switch
    {
        ForestNodeContent.Empty => null,
        ForestNodeContent.NormalCombat => RoomType.Monster,
        ForestNodeContent.EliteCombat => RoomType.Elite,
        ForestNodeContent.Event => RoomType.Event,
        ForestNodeContent.Reward => RoomType.Treasure,
        ForestNodeContent.Shop => RoomType.Shop,
        ForestNodeContent.Door => null,
        ForestNodeContent.BattleSavage => RoomType.Monster,
        ForestNodeContent.Light => null,
        ForestNodeContent.Boss => RoomType.Boss,
        ForestNodeContent.Final => null,
        ForestNodeContent.Evacuate => null,
        _ => throw new InvalidDataException("Unknown node content.")
    };
}
