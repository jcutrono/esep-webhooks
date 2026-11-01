using System.Text;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EsepWebhook;

public class Function
{
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public string FunctionHandler(object input, ILambdaContext context)
    {
        context.Logger.LogInformation($"FunctionHandler received: {input}");

        dynamic json = JsonConvert.DeserializeObject<dynamic>(input.ToString());

        // Testing from postman, you can use this code to test the function
        /*
        context.Logger.LogInformation($"Body: {json.body}");
        dynamic body = JsonConvert.DeserializeObject<dynamic>(json.body.ToString());
        context.Logger.LogInformation($"Issue: {body.issue}");
        context.Logger.LogInformation($"Html: {body.issue.html_url}");
        string payload = $"{{'text':'Issue Created: {body.issue.html_url}'}}";
        */
        
        string payload = JsonConvert.SerializeObject(new { text = $"Issue Created: {json.issue.html_url}" });

        var client = new HttpClient();
        var webRequest = new HttpRequestMessage(HttpMethod.Post, Environment.GetEnvironmentVariable("SLACK_URL"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    
        var response = client.Send(webRequest);
        using var reader = new StreamReader(response.Content.ReadAsStream());
            
        return reader.ReadToEnd();
    }
}
