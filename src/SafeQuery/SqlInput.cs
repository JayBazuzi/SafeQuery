namespace SafeQuery;

internal static class SqlInput
{
    internal static string? Resolve(string[] args, TextReader stdin, bool stdinRedirected)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-f")
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("-f flag requires a file path argument");
                }

                var path = args[i + 1];
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException($"SQL file not found: {path}");
                }

                return File.ReadAllText(path);
            }
        }

        var positional = args.FirstOrDefault(a => a != "-f");
        if (!string.IsNullOrWhiteSpace(positional))
        {
            return positional;
        }

        if (stdinRedirected)
        {
            return stdin.ReadToEnd();
        }

        throw new InvalidOperationException(
            "No SQL provided. Pass SQL as an argument, use -f <path>, or pipe SQL via stdin.");
    }
}
