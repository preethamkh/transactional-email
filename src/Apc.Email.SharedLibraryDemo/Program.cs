using Apc.Email.Contracts;
using Apc.Email.Mandrill;
using System.Net.Http.Json;

var apiKey = Environment.GetEnvironmentVariable("MANDRILL_API_KEY");
var toEmail = Environment.GetEnvironmentVariable("DEMO_TO_EMAIL");
var fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL") ?? "info@physiocouncil.com.au";
var centralApiUrl = Environment.GetEnvironmentVariable("CENTRAL_API_URL");

if (string.IsNullOrWhiteSpace(toEmail))
{
    Console.WriteLine("Set DEMO_TO_EMAIL to run a real send.");
    return;
}

var request = new EmailRequest("assessment-complex", [new EmailRecipient(toEmail, "Demo recipient")],
    new Dictionary<string, object?>
    {
        ["candidateName"] = "Dr. Preetham K H",
        ["assessmentDate"] = "Monday, 25 August 2026",
        ["results"] = new[]
        {
            new { area = "Mobility", score = 4, observations = new[] { "Stable gait", "Full range" } },
            new { area = "Strength", score = 5, observations = new[] { "Good resistance", "No pain" } }
        }
    }, "shared-library-demo");

if (!string.IsNullOrWhiteSpace(centralApiUrl))
{
    using var centralClient = new HttpClient();
    centralClient.DefaultRequestHeaders.Add("X-Api-Key", Environment.GetEnvironmentVariable("DEMO_API_KEY") ?? "demo-key");
    var response = await centralClient.PostAsJsonAsync($"{centralApiUrl.TrimEnd('/')}/api/v1/email/send", request);
    Console.WriteLine($"Central API send: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
}
else
{
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.WriteLine("Set MANDRILL_API_KEY or CENTRAL_API_URL to run a real send.");
        return;
    }
    using var httpClient = new HttpClient();
    var sender = new MandrillEmailSender(httpClient, apiKey, fromEmail);
    var result = await sender.SendTemplateAsync(request, "assessment-complex");
    Console.WriteLine($"Shared library direct send: {result.Status}; correlation={result.CorrelationId}; providerId={result.ProviderMessageId}");
}
