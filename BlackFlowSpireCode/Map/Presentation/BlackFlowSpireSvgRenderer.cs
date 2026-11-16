using System.Security;
using System.Text;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Presentation;

public static class BlackFlowSpireSvgRenderer
{
    public static string Render(BlackFlowSpireMap map, bool revealAll = false, string? bossTitle = null)
    {
        BlackFlowSpireMapValidator.Validate(map);
        const int spacing = 106;
        const int margin = 88;
        const int top = 170;
        var width = margin * 2 + (map.Options.Width - 1) * spacing;
        var height = top + (map.Options.Height - 1) * spacing + 100;
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        (int X, int Y) Position(BlackFlowSpireNode node) =>
            (margin + node.X * spacing, top + (map.Options.Height - 1 - node.Y) * spacing);
        var svg = new StringBuilder();
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" role=\"img\">");
        svg.AppendLine("<style>text{font-family:'Microsoft YaHei','Noto Sans CJK SC',sans-serif;fill:#e5ede7} .label{font-size:12px;text-anchor:middle} .edge{stroke:#53695a;stroke-width:3;stroke-linecap:round}</style>");
        svg.AppendLine($"<rect width=\"{width}\" height=\"{height}\" fill=\"#10231d\"/>");
        svg.AppendLine($"<text x=\"40\" y=\"48\" font-size=\"28\" font-weight=\"bold\">{Escape(BlackFlowSpireText.Get(revealAll ? "PREVIEW.title.revealed" : "PREVIEW.title.fog"))}</text>");
        svg.AppendLine($"<text x=\"40\" y=\"80\" font-size=\"14\">{Escape(BlackFlowSpireText.Get("PREVIEW.summary", ("Seed", map.Seed), ("Act", map.ActIndex + 1), ("Count", map.Nodes.Count), ("Amount", map.Options.InitialActionPoints)))}</text>");
        svg.AppendLine($"<text x=\"40\" y=\"108\" font-size=\"13\" fill=\"#a0b5a7\">{Escape(BlackFlowSpireText.Get("PREVIEW.reveal", ("Position", map.PositionNodeId), ("Steps", map.Options.RevealRadius)))}</text>");
        foreach (var node in map.Nodes)
        foreach (var neighbor in node.Neighbors.Where(n => n > node.Id))
        {
            var a = Position(node);
            var b = Position(nodes[neighbor]);
            svg.AppendLine($"<line class=\"edge\" x1=\"{a.X}\" y1=\"{a.Y}\" x2=\"{b.X}\" y2=\"{b.Y}\"/>");
        }
        foreach (var node in map.Nodes)
        {
            var point = Position(node);
            var view = BlackFlowSpireNodePresentation.Get(node, revealAll, bossTitle, map);
            var start = node.Id == map.StartNodeId;
            var current = node.Id == map.PositionNodeId;
            var title = start ? BlackFlowSpireText.Get(current ? "PREVIEW.current_start" : "UI.STATUS.start")
                : string.IsNullOrEmpty(view.Title) ? view.Type : view.Title;
            var color = current ? "#8dd8b8" : node.Kind switch
            {
                BlackFlowSpireNodeKind.Combat => "#e8a390",
                BlackFlowSpireNodeKind.Event => "#b8b0e6",
                BlackFlowSpireNodeKind.Boss => "#f0cf7f",
                BlackFlowSpireNodeKind.Exit => "#7dd9aa",
                _ => "#819e8a"
            };
            svg.AppendLine($"<g transform=\"translate({point.X},{point.Y})\"><title>{Escape(title)} ({node.X},{node.Y})</title>");
            if (current)
                svg.AppendLine("<circle r=\"29\" fill=\"none\" stroke=\"#8dd8b8\" stroke-width=\"2\"/>");
            svg.AppendLine($"<circle r=\"23\" fill=\"#142a22\" stroke=\"{color}\" stroke-width=\"{(start || current || node.Kind == BlackFlowSpireNodeKind.Boss ? 3 : 2)}\"/>");
            var symbol = start ? "S" : view.IconKey switch
            {
                BlackFlowSpireNodeVisuals.EMPTY => "·", BlackFlowSpireNodeVisuals.HIDE_BATTLE => "⚔", BlackFlowSpireNodeVisuals.HIDE_INVISIBLE => "?",
                BlackFlowSpireNodeVisuals.BATTLE_NORMAL => "⚔", BlackFlowSpireNodeVisuals.BATTLE_ELITE => "E", BlackFlowSpireNodeVisuals.INCIDENT => "?",
                BlackFlowSpireNodeVisuals.WISH => "+", BlackFlowSpireNodeVisuals.BATTLE_SHOP => "$", BlackFlowSpireNodeVisuals.BATTLE_BOSS => "B",
                BlackFlowSpireNodeVisuals.FINAL => ">",
                BlackFlowSpireNodeVisuals.DOOR => "↔", BlackFlowSpireNodeVisuals.LIGHT => "V", BlackFlowSpireNodeVisuals.BATTLE_SAVAGE => "R",
                BlackFlowSpireNodeVisuals.BATTLE_SAVAGE_BUOY => "!", _ => "·"
            };
            svg.AppendLine($"<text text-anchor=\"middle\" y=\"7\" font-size=\"22\" font-weight=\"bold\" style=\"fill:{color}\">{symbol}</text>");
            svg.AppendLine($"<text class=\"label\" y=\"43\">{Escape(title)}</text></g>");
        }
        svg.AppendLine($"<text x=\"40\" y=\"{height - 25}\" font-size=\"12\">{Escape(BlackFlowSpireText.Get("PREVIEW.footer"))}</text></svg>");
        return svg.ToString();
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
}
