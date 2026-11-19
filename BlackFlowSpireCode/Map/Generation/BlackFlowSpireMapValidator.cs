using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Stages;

namespace BlackFlowSpire.Map.Generation;

public static class BlackFlowSpireMapValidator
{
    public static void Validate(BlackFlowSpireMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.SchemaVersion != 1 || map.GeneratorVersion != 1)
            throw new InvalidDataException("Unsupported forest map version.");
        ArgumentException.ThrowIfNullOrWhiteSpace(map.Seed);
        if (map.ActIndex < 0) throw new InvalidDataException("Invalid act index.");
        map.Options.Validate();
        if (map.Nodes.Count != map.Options.NodeCount)
            throw new InvalidDataException("Map node count differs from generation settings.");
        if (map.Nodes.Select(n => n.Id).Distinct().Count() != map.Nodes.Count)
            throw new InvalidDataException("Duplicate node IDs.");
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        if (map.Nodes.Count(node => node.Content == BlackFlowSpireNodeContent.Door) is not (0 or 2))
            throw new InvalidDataException("A map may contain no passages or exactly one passage pair.");
        if (map.Nodes.Count(node => node.Content == BlackFlowSpireNodeContent.Evacuate) > 1)
            throw new InvalidDataException("A map may contain at most one floor shortcut.");
        if (!nodes.TryGetValue(map.StartNodeId, out var start) || !nodes.TryGetValue(map.BossNodeId, out var boss))
            throw new InvalidDataException("Missing start or Boss node.");
        if (!nodes.ContainsKey(map.PositionNodeId))
            throw new InvalidDataException("Current position does not exist in the map.");
        if (start.X != map.Options.Width / 2 || start.Y != map.Options.Height / 2
            || start.Kind != BlackFlowSpireNodeKind.Empty || start.Content != BlackFlowSpireNodeContent.Empty)
            throw new InvalidDataException("Starting node must be an empty node at the center.");
        var exitCount = map.Nodes.Count(n => n.Kind is BlackFlowSpireNodeKind.Boss or BlackFlowSpireNodeKind.Exit);
        var hasExit = exitCount == 1 && (boss.Kind == BlackFlowSpireNodeKind.Boss && boss.Content == BlackFlowSpireNodeContent.Boss
            || boss.Kind == BlackFlowSpireNodeKind.Exit && boss.Content == BlackFlowSpireNodeContent.Final);
        var consumedBoss = boss.Kind == BlackFlowSpireNodeKind.Empty && boss.Content == BlackFlowSpireNodeContent.Empty
            && boss.Completed && exitCount == 0;
        if ((!hasExit && !consumedBoss) || boss.Neighbors.Count != 1
            || (boss.X != 0 && boss.Y != 0 && boss.X != map.Options.Width - 1 && boss.Y != map.Options.Height - 1))
            throw new InvalidDataException("The exit must be a single terminal Boss, Final or consumed empty node at the boundary.");
        var bossDistance = Math.Abs(boss.X - start.X) + Math.Abs(boss.Y - start.Y);
        if (bossDistance < map.Options.MinimumBossDistance)
            throw new InvalidDataException("Boss is too close to the center.");
        foreach (var node in map.Nodes)
        {
            if (node.X < 0 || node.X >= map.Options.Width || node.Y < 0 || node.Y >= map.Options.Height
                || node.Id != node.Y * map.Options.Width + node.X)
                throw new InvalidDataException("Invalid node coordinate or ID.");
            var contentMatches = node.Kind switch
            {
                BlackFlowSpireNodeKind.Empty => node.Content == BlackFlowSpireNodeContent.Empty,
                BlackFlowSpireNodeKind.Combat => node.Content is BlackFlowSpireNodeContent.NormalCombat or BlackFlowSpireNodeContent.EliteCombat
                    or BlackFlowSpireNodeContent.BattleSavage,
                BlackFlowSpireNodeKind.Event => node.Content is BlackFlowSpireNodeContent.Event or BlackFlowSpireNodeContent.Reward
                    or BlackFlowSpireNodeContent.Shop or BlackFlowSpireNodeContent.Door or BlackFlowSpireNodeContent.Light
                    or BlackFlowSpireNodeContent.Evacuate or BlackFlowSpireNodeContent.Rest or BlackFlowSpireNodeContent.Sacrifice
                    or BlackFlowSpireNodeContent.Expedition,
                BlackFlowSpireNodeKind.Boss => node.Content == BlackFlowSpireNodeContent.Boss,
                BlackFlowSpireNodeKind.Exit => node.Content == BlackFlowSpireNodeContent.Final,
                _ => false
            };
            if (!contentMatches) throw new InvalidDataException("Node content does not match its category.");
            if (node.Content is BlackFlowSpireNodeContent.Final or BlackFlowSpireNodeContent.Evacuate
                && !BlackFlowSpireFloorExitRules.IsExitFloor(map.ActIndex))
                throw new InvalidDataException("Only floors one and three may contain floor-exit choices.");
            if (node.CreatedByCompletion && node.Kind != BlackFlowSpireNodeKind.Empty)
                throw new InvalidDataException("A clearing produced by a completed encounter must remain empty.");
            if (node.VantageActivated && node.Content != BlackFlowSpireNodeContent.Light)
                throw new InvalidDataException("Only a vantage point may retain its activation state.");
            if (node.Content == BlackFlowSpireNodeContent.Door)
            {
                if (!node.Repeatable || !node.CanPassThrough || node.PairedNodeId is not { } pairedId
                    || pairedId == node.Id || !nodes.TryGetValue(pairedId, out var paired)
                    || paired.Content != BlackFlowSpireNodeContent.Door || paired.PairedNodeId != node.Id)
                    throw new InvalidDataException("Passages must form distinct reciprocal, repeatable, passable pairs.");
            }
            else if (node.PairedNodeId.HasValue)
                throw new InvalidDataException("Only passage nodes may have a paired destination.");
            if (node.CombatSetup is { } setup
                && (node.Kind is not (BlackFlowSpireNodeKind.Combat or BlackFlowSpireNodeKind.Boss)
                    || string.IsNullOrWhiteSpace(setup.EncounterId) || setup.Enemies == null || setup.Enemies.Count == 0
                    || setup.Enemies.Any(enemy => enemy == null || string.IsNullOrWhiteSpace(enemy.MonsterId))))
                throw new InvalidDataException("Invalid saved enemy formation.");
            if (node.CombatSetup?.Stage is { } stage)
            {
                stage.Validate();
                if (stage.SourceEncounterId != node.CombatSetup.EncounterId)
                    throw new InvalidDataException("Stage version and fixed source encounter differ.");
                var expected = node.Content switch
                {
                    BlackFlowSpireNodeContent.NormalCombat => BlackFlowSpireBattleVariant.Normal,
                    BlackFlowSpireNodeContent.EliteCombat => BlackFlowSpireBattleVariant.Emergency,
                    BlackFlowSpireNodeContent.Boss => BlackFlowSpireBattleVariant.Boss,
                    BlackFlowSpireNodeContent.BattleSavage => stage.Variant,
                    _ => throw new InvalidDataException("Non-combat node has a stage snapshot.")
                };
                if (stage.Variant != expected) throw new InvalidDataException("Stage variant differs from its map node.");
            }
            if (node.Neighbors.Count == 0 || node.Neighbors.Distinct().Count() != node.Neighbors.Count)
                throw new InvalidDataException("Isolated node or duplicate edges.");
            foreach (var id in node.Neighbors)
            {
                if (!nodes.TryGetValue(id, out var other) || !other.Neighbors.Contains(node.Id)
                    || Math.Abs(node.X - other.X) + Math.Abs(node.Y - other.Y) != 1)
                    throw new InvalidDataException("An edge is missing, asymmetric, or not four-way adjacent.");
            }
        }
        if (map.ResidentMovementCount < 0 || map.RoamingResidents.Count > 3
            || map.RoamingResidents.Select(resident => resident.Id).Distinct().Count() != map.RoamingResidents.Count
            || map.RoamingResidents.Select(resident => resident.NodeId).Distinct().Count() != map.RoamingResidents.Count)
            throw new InvalidDataException("Invalid roaming resident count, identity or occupied positions.");
        foreach (var resident in map.RoamingResidents)
        {
            if (resident.Id < 0 || !nodes.TryGetValue(resident.StrongholdNodeId, out var home)
                || home.Content != BlackFlowSpireNodeContent.BattleSavage || home.Completed
                || !nodes.TryGetValue(resident.NodeId, out var occupied) || occupied.Repeatable
                || occupied.CreatedByCompletion || occupied.Kind is BlackFlowSpireNodeKind.Boss or BlackFlowSpireNodeKind.Exit
                || occupied.Id == map.BossNodeId || occupied.Content == BlackFlowSpireNodeContent.BattleSavage)
                throw new InvalidDataException("A roaming resident occupies a protected node or lacks an active stronghold.");
            if (resident.CombatSetup is { } setup)
            {
                if (string.IsNullOrWhiteSpace(setup.EncounterId) || setup.Enemies == null || setup.Enemies.Count == 0
                    || setup.Enemies.Any(enemy => enemy == null || string.IsNullOrWhiteSpace(enemy.MonsterId)))
                    throw new InvalidDataException("Invalid saved roaming resident formation.");
                setup.Stage?.Validate();
                if (setup.Stage != null && setup.Stage.SourceEncounterId != setup.EncounterId)
                    throw new InvalidDataException("Roaming resident stage and encounter differ.");
            }
        }
        var reached = new HashSet<int> { start.Id };
        var pending = new Queue<int>();
        pending.Enqueue(start.Id);
        while (pending.TryDequeue(out var id))
            foreach (var neighbor in nodes[id].Neighbors)
                if (reached.Add(neighbor)) pending.Enqueue(neighbor);
        if (reached.Count != map.Nodes.Count)
            throw new InvalidDataException("Map contains disconnected nodes.");
        var steps = map.FindShortestPath(start.Id, boss.Id).Count - 1;
        if (steps < 1 || (long)steps * map.Options.MoveCost > map.Options.InitialActionPoints)
            throw new InvalidDataException("Boss is not reachable within the initial action-point budget.");
    }
}
