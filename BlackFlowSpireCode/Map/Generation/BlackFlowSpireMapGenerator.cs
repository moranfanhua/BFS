using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Generation;

public static class BlackFlowSpireMapGenerator
{
    public static BlackFlowSpireMap Generate(string seed, int actIndex = 0, BlackFlowSpireGenerationOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (seed.Length > 256)
            throw new ArgumentException("Seed must not exceed 256 characters.", nameof(seed));
        ArgumentOutOfRangeException.ThrowIfNegative(actIndex);
        var floor = BlackFlowSpireFloorPlan.Get(actIndex);
        var settings = options ?? BlackFlowSpireFloorPlan.GenerationOptions(actIndex);
        settings.Validate();
        var layout = new StableRandom(seed, actIndex, "layout");
        var content = new StableRandom(seed, actIndex, "content");
        var startX = settings.Width / 2;
        var startY = settings.Height / 2;
        var startId = startY * settings.Width + startX;

        // Pick only boundary cells whose direct four-way route fits both budgets.
        var candidates = new List<int>();
        for (var y = 0; y < settings.Height; y++)
        for (var x = 0; x < settings.Width; x++)
        {
            if (x != 0 && y != 0 && x != settings.Width - 1 && y != settings.Height - 1)
                continue;
            var distance = Math.Abs(x - startX) + Math.Abs(y - startY);
            if (distance >= settings.MinimumBossDistance && distance < settings.NodeCount
                && (long)distance * settings.MoveCost <= settings.InitialActionPoints)
                candidates.Add(y * settings.Width + x);
        }
        if (candidates.Count == 0)
            throw new ArgumentException("No boundary Boss can satisfy the distance, node count, and action-point budget.", nameof(options));
        var bossId = candidates[layout.Next(candidates.Count)];
        var nodes = new SortedDictionary<int, BlackFlowSpireNode>();

        BlackFlowSpireNode AddNode(int id)
        {
            var node = new BlackFlowSpireNode { Id = id, X = id % settings.Width, Y = id / settings.Width };
            nodes.Add(id, node);
            return node;
        }

        AddNode(startId);
        var current = startId;
        var bossX = bossId % settings.Width;
        var bossY = bossId / settings.Width;
        // A randomized monotone main route guarantees reachability without regeneration loops.
        while (current != bossId)
        {
            var x = current % settings.Width;
            var y = current / settings.Width;
            var steps = new List<int>(2);
            if (x != bossX) steps.Add(current + Math.Sign(bossX - x));
            if (y != bossY) steps.Add(current + Math.Sign(bossY - y) * settings.Width);
            var next = steps[layout.Next(steps.Count)];
            AddNode(next);
            Connect(nodes[current], nodes[next]);
            current = next;
        }

        // Grow connected branches. Boss is a terminal node, never a bridge to other rooms.
        while (nodes.Count < settings.NodeCount)
        {
            var frontier = new SortedSet<int>();
            foreach (var node in nodes.Values)
            {
                if (node.Id == bossId) continue;
                foreach (var neighbor in GetAdjacentIds(node.Id, settings.Width, settings.Height))
                    if (!nodes.ContainsKey(neighbor)) frontier.Add(neighbor);
            }
            if (frontier.Count == 0)
                throw new InvalidOperationException("Connected map growth unexpectedly exhausted its frontier.");
            var id = frontier.ElementAt(layout.Next(frontier.Count));
            var parents = GetAdjacentIds(id, settings.Width, settings.Height)
                .Where(n => n != bossId && nodes.ContainsKey(n)).Order().ToList();
            Connect(AddNode(id), nodes[parents[layout.Next(parents.Count)]]);
        }

        var extraEdges = new List<(int A, int B)>();
        foreach (var node in nodes.Values)
        foreach (var neighbor in GetAdjacentIds(node.Id, settings.Width, settings.Height))
        {
            if (node.Id < neighbor && node.Id != bossId && neighbor != bossId
                && nodes.ContainsKey(neighbor) && !node.Neighbors.Contains(neighbor))
                extraEdges.Add((node.Id, neighbor));
        }
        layout.Shuffle(extraEdges);
        foreach (var (a, b) in extraEdges.Take(settings.ExtraConnections))
            Connect(nodes[a], nodes[b]);

        var ordinaryNodes = nodes.Values.Where(n => n.Id != startId && n.Id != bossId).ToList();
        var kinds = Allocate(ordinaryNodes.Count, content,
            (BlackFlowSpireNodeKind.Empty, settings.EmptyWeight),
            (BlackFlowSpireNodeKind.Combat, settings.CombatWeight),
            (BlackFlowSpireNodeKind.Event, settings.EventWeight));
        for (var i = 0; i < ordinaryNodes.Count; i++)
            ordinaryNodes[i].Kind = kinds[i];

        var combatNodes = ordinaryNodes.Where(n => n.Kind == BlackFlowSpireNodeKind.Combat).ToList();
        var combatContents = Allocate(combatNodes.Count, content,
            (BlackFlowSpireNodeContent.NormalCombat, 100 - settings.ElitePercent),
            (BlackFlowSpireNodeContent.EliteCombat, settings.ElitePercent));
        for (var i = 0; i < combatNodes.Count; i++)
            combatNodes[i].Content = combatContents[i];

        var eventNodes = ordinaryNodes.Where(n => n.Kind == BlackFlowSpireNodeKind.Event).ToList();
        var eventContents = Allocate(eventNodes.Count, content,
            (BlackFlowSpireNodeContent.Event, settings.RegularEventWeight),
            (BlackFlowSpireNodeContent.Reward, settings.RewardWeight),
            (BlackFlowSpireNodeContent.Shop, settings.ShopWeight));
        for (var i = 0; i < eventNodes.Count; i++)
            eventNodes[i].Content = eventContents[i];

        // From floor two, both trader variants have equal weight within the
        // existing shop allocation. No floor is forced to contain either type.
        if (actIndex >= 1)
            foreach (var trader in eventNodes.Where(node => node.Content == BlackFlowSpireNodeContent.Shop))
                if (content.Next(2) == 0)
                    trader.Details = new BlackFlowSpireNodeDetailsSource { VisualId = BlackFlowSpireNodeVisuals.SCRAP_SHOP };

        nodes[startId].Revealed = true;
        nodes[startId].Completed = true;
        nodes[bossId].Kind = floor.ExitContent == BlackFlowSpireNodeContent.Boss ? BlackFlowSpireNodeKind.Boss : BlackFlowSpireNodeKind.Exit;
        nodes[bossId].Content = floor.ExitContent;
        nodes[bossId].Revealed = true;
        foreach (var node in nodes.Values) node.Neighbors.Sort();

        var map = new BlackFlowSpireMap
        {
            Seed = seed, ActIndex = actIndex, Options = settings,
            StartNodeId = startId, BossNodeId = bossId, PositionNodeId = startId, Nodes = nodes.Values.ToList()
        };
        BlackFlowSpireFloorExitRules.Populate(map);
        BlackFlowSpirePassageAssignments.Populate(map);
        BlackFlowSpireVantageAssignments.Populate(map);
        BlackFlowSpireResidentAssignments.Populate(map);
        BlackFlowSpireFog.RevealNearby(map);
        foreach (var node in map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Light))
            BlackFlowSpireVantageRules.RevealOnAppearance(map, node);
        return map;
    }

    // Largest-remainder allocation keeps counts within one node of requested proportions.
    private static List<T> Allocate<T>(int count, StableRandom random, params (T Value, int Weight)[] choices)
    {
        var total = choices.Sum(c => (long)c.Weight);
        var counts = choices.Select(c => (int)((long)count * c.Weight / total)).ToArray();
        var remaining = count - counts.Sum();
        var remainderOrder = choices.Select((c, i) => (Index: i, Remainder: (long)count * c.Weight % total))
            .OrderByDescending(c => c.Remainder).ThenBy(c => c.Index).ToList();
        foreach (var entry in remainderOrder.Take(remaining)) counts[entry.Index]++;
        var bag = new List<T>(count);
        for (var i = 0; i < choices.Length; i++)
            for (var j = 0; j < counts[i]; j++) bag.Add(choices[i].Value);
        random.Shuffle(bag);
        return bag;
    }

    internal static IEnumerable<int> GetAdjacentIds(int id, int width, int height)
    {
        var x = id % width;
        var y = id / width;
        if (x > 0) yield return id - 1;
        if (x < width - 1) yield return id + 1;
        if (y > 0) yield return id - width;
        if (y < height - 1) yield return id + width;
    }

    private static void Connect(BlackFlowSpireNode a, BlackFlowSpireNode b)
    {
        a.Neighbors.Add(b.Id);
        b.Neighbors.Add(a.Id);
    }
}
