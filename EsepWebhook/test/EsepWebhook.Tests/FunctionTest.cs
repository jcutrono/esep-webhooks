using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;
using Amazon.Lambda.TestUtilities;

namespace EsepWebhook.Tests;

public class FunctionTest
{
    // Trimmed GitHub "issues" webhook body, as API Gateway (non-proxy POST on /) passes it to the Lambda.
    private const string SampleIssueEvent = """
        {
          "action": "opened",
          "issue": {
            "url": "https://api.github.com/repos/jcutrono/esep-webhooks/issues/2",
            "repository_url": "https://api.github.com/repos/jcutrono/esep-webhooks",
            "html_url": "https://github.com/jcutrono/esep-webhooks/issues/2",
            "id": 1402369902,
            "number": 2,
            "title": "test",
            "user": { "login": "jcutrono" }
          }
        }
        """;

    [Fact]
    public async Task PostsIssueUrlToSlackAsJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Stand in for Slack with a local listener so the test never calls a real webhook.
        var slackUrl = $"http://localhost:{GetFreePort()}/";
        using var fakeSlack = new HttpListener();
        fakeSlack.Prefixes.Add(slackUrl);
        fakeSlack.Start();

        var receivedBody = Task.Run(async () =>
        {
            var request = await fakeSlack.GetContextAsync();
            using var reader = new StreamReader(request.Request.InputStream);
            var body = await reader.ReadToEndAsync(cancellationToken);
            await request.Response.OutputStream.WriteAsync("ok"u8.ToArray(), cancellationToken);
            request.Response.Close();
            return body;
        }, cancellationToken);

        Environment.SetEnvironmentVariable("SLACK_URL", slackUrl);
        try
        {
            // The Lambda runtime hands the handler a JsonElement for an "object" parameter.
            var input = JsonDocument.Parse(SampleIssueEvent).RootElement;

            var result = new Function().FunctionHandler(input, new TestLambdaContext());

            Assert.Equal("ok", result);

            // Slack only accepts strict JSON, so parse with System.Text.Json (which rejects single quotes).
            using var payload = JsonDocument.Parse(await receivedBody);
            Assert.Equal(
                "Issue Created: https://github.com/jcutrono/esep-webhooks/issues/2",
                payload.RootElement.GetProperty("text").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLACK_URL", null);
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
