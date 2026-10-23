using System.Text.RegularExpressions;

namespace SafeQuery;

internal static class DiagramText
{
    // SqlServerToMermaid output that Mermaid cannot parse: commas inside types (e.g. "decimal(18,2)").
    internal static string MakeMermaidParseable(string markdown)
    {
        return Regex.Replace(markdown, @"\((\d+),(\d+)\)", "($1-$2)");
    }

    // Relationship labels (long FK constraint names) make the diagram very wide, so they are blanked.
    internal static string BlankRelationshipLabels(string markdown)
    {
        return Regex.Replace(markdown, @"(\|\|--o\{ ""[^""]+"") : ""[^""]*""", "$1 : \"\"");
    }

    internal static string ExtractMermaid(string markdown)
    {
        var match = Regex.Match(markdown, @"```mermaid\s*\r?\n(.*?)```", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : markdown;
    }
}
