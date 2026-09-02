using Apc.Email.Contracts;
using Apc.Email.Mandrill;

var apiKey = Environment.GetEnvironmentVariable("MANDRILL_API_KEY");
var toEmail = Environment.GetEnvironmentVariable("DEMO_TO_EMAIL");
var fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL") ?? "info@physiocouncil.com.au";

if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(toEmail))
{
    Console.WriteLine("Set MANDRILL_API_KEY and DEMO_TO_EMAIL to run a real send.");
    Console.WriteLine("The shared-library path is demonstrated by the compiled MandrillEmailSender package.");
    return;
}

using var httpClient = new HttpClient();
var sender = new MandrillEmailSender(httpClient, apiKey, fromEmail);
//var request = new EmailRequest("AssessmentBooked", [new EmailRecipient(toEmail, "Demo recipient")],
//    new Dictionary<string, object?>
//    {
//        ["candidate"] = "Jane Example",
//        ["assessment"] = "Capability Assessment",
//        ["session"] = new { date = "Monday, 25 August 2026", location = "Melbourne" },
//        ["practitioner"] = "Dr. Preetham K H" 
//    }, "shared-library-demo");


var request = new EmailRequest("MailchimpToMandrill", [new EmailRecipient(toEmail, "Demo recipient")],
    new Dictionary<string, object?>
    {
        ["fname"] = "Dr. Preetham K H"
    }, "shared-library-demo");
var result = await sender.SendTemplateAsync(request, "Mailchimp-to-Mandrill");
Console.WriteLine($"Shared library send: {result.Status}; correlation={result.CorrelationId}; providerId={result.ProviderMessageId}");
