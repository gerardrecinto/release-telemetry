using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DeployMarker;

public record Deployment(string EntityGuid, string Version, string? Commit = null, string? User = null);

public class MarkerException : Exception
{
    public MarkerException(string message) : base(message) { }
}

public static class Marker
{
    public const string DefaultEndpoint = "https://api.newrelic.com/graphql";

    // NerdGraph change tracking: records a deployment on an entity so it shows up on charts.
    const string Mutation = "mutation($d: ChangeTrackingDeploymentInput!) { changeTrackingCreateDeployment(deployment: $d) { deploymentId } }";

    public static string BuildBody(Deployment d)
    {
        var input = new Dictionary<string, string?> { ["entityGuid"] = d.EntityGuid, ["version"] = d.Version, ["commit"] = d.Commit, ["user"] = d.User };
        var clean = input.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
        return JsonSerializer.Serialize(new { query = Mutation, variables = new { d = clean } });
    }

    public static async Task<string> SendAsync(HttpClient http, string endpoint, string apiKey, Deployment d)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(BuildBody(d), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        req.Headers.Add("API-Key", apiKey);
        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new MarkerException($"New Relic answered {(int)res.StatusCode}");
        using var doc = JsonDocument.Parse(text);
        // NerdGraph reports many failures as HTTP 200 with an "errors" array, so a 200 alone is not success.
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            throw new MarkerException("New Relic rejected the deployment: " + errors[0].GetProperty("message").GetString());
        return doc.RootElement.GetProperty("data").GetProperty("changeTrackingCreateDeployment").GetProperty("deploymentId").GetString()!;
    }
}
