using System.Net;
using System.Text.Json;
using DeployMarker;

namespace DeployMarker.Tests;

public class MarkerTests
{
    // A one-request local server that records what it received and answers with a fixed status and body.
    sealed class Fake : IDisposable
    {
        readonly HttpListener _l = new();
        public string Url { get; }
        public string? Body, ApiKey;
        public Fake(int status, string reply)
        {
            var port = new Random().Next(20000, 40000);
            Url = $"http://127.0.0.1:{port}/graphql";
            _l.Prefixes.Add($"http://127.0.0.1:{port}/");
            _l.Start();
            Task.Run(async () =>
            {
                var c = await _l.GetContextAsync();
                Body = await new StreamReader(c.Request.InputStream).ReadToEndAsync();
                ApiKey = c.Request.Headers["API-Key"];
                c.Response.StatusCode = status;
                var bytes = System.Text.Encoding.UTF8.GetBytes(reply);
                await c.Response.OutputStream.WriteAsync(bytes);
                c.Response.Close();
            });
        }
        public void Dispose() => _l.Stop();
    }

    static readonly Deployment D = new("GUID123", "1.4.2", "abc123", "gerard");

    [Fact]
    public void Body_has_version_commit_and_entity_and_skips_empty_fields()
    {
        var v = JsonDocument.Parse(Marker.BuildBody(new Deployment("G", "2.0.0"))).RootElement.GetProperty("variables").GetProperty("d");
        Assert.Equal("2.0.0", v.GetProperty("version").GetString());
        Assert.False(v.TryGetProperty("commit", out _));
    }

    [Fact]
    public async Task Sends_the_key_header_and_returns_the_deployment_id()
    {
        using var f = new Fake(200, "{\"data\":{\"changeTrackingCreateDeployment\":{\"deploymentId\":\"dep-1\"}}}");
        var id = await Marker.SendAsync(new HttpClient(), f.Url, "KEY", D);
        Assert.Equal("dep-1", id);
        Assert.Equal("KEY", f.ApiKey);
        Assert.Contains("1.4.2", f.Body);
    }

    [Fact]
    public async Task A_200_with_graphql_errors_is_a_failure()
    {
        using var f = new Fake(200, "{\"errors\":[{\"message\":\"entity not found\"}],\"data\":null}");
        var e = await Assert.ThrowsAsync<MarkerException>(() => Marker.SendAsync(new HttpClient(), f.Url, "KEY", D));
        Assert.Contains("entity not found", e.Message);
    }

    [Fact]
    public async Task A_non_success_status_is_a_failure_without_echoing_the_key()
    {
        using var f = new Fake(403, "forbidden");
        var e = await Assert.ThrowsAsync<MarkerException>(() => Marker.SendAsync(new HttpClient(), f.Url, "SECRETKEY", D));
        Assert.Contains("403", e.Message);
        Assert.DoesNotContain("SECRETKEY", e.Message);
    }
}
