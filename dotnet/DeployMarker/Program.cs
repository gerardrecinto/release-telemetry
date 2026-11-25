using DeployMarker;

static string? Arg(string[] a, string name) => Array.IndexOf(a, name) is var i and >= 0 && i + 1 < a.Length ? a[i + 1] : null;

if (args.Contains("--help") || Arg(args, "--entity-guid") is null || Arg(args, "--version") is null)
{
    Console.Error.WriteLine("usage: DeployMarker --entity-guid GUID --version V [--commit SHA] [--user NAME] [--endpoint URL] [--dry-run]\nSet NEW_RELIC_API_KEY unless --dry-run.");
    return args.Contains("--help") ? 0 : 2;
}

var deployment = new Deployment(Arg(args, "--entity-guid")!, Arg(args, "--version")!, Arg(args, "--commit"), Arg(args, "--user"));
if (args.Contains("--dry-run"))
{
    Console.WriteLine(Marker.BuildBody(deployment));
    return 0;
}

var key = Environment.GetEnvironmentVariable("NEW_RELIC_API_KEY");
if (string.IsNullOrEmpty(key))
{
    Console.Error.WriteLine("NEW_RELIC_API_KEY is not set. Use --dry-run to print the request without sending it.");
    return 2;
}

try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    var id = await Marker.SendAsync(http, Arg(args, "--endpoint") ?? Marker.DefaultEndpoint, key, deployment);
    Console.WriteLine($"recorded deployment {id}");
    return 0;
}
catch (Exception e) when (e is MarkerException or HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
