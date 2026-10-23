namespace SafeQuery;

internal static class EnvironmentOption
{
    // Removes "--env <name>" from args so the rest parse as before.
    internal static (string? Name, string[] Remaining) Extract(string[] args)
    {
        var index = Array.IndexOf(args, "--env");
        if (index < 0)
        {
            return (null, args);
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith('-') || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new InvalidOperationException("--env flag requires an environment name");
        }

        return (args[index + 1], [.. args[..index], .. args[(index + 2)..]]);
    }
}
