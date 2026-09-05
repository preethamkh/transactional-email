using System.Net.Http.Json;
using TransactionalEmail.Contracts;

namespace TransactionalEmail.Client;

public sealed class EmailApiClient(HttpClient httpClient)
{
    public async Task<EmailSendResult?> SendAsync(EmailRequest request, string apiKey, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/email/send")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Source-System", request.SourceSystem);
        message.Headers.Add("X-Api-Key", apiKey);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmailSendResult>(cancellationToken: cancellationToken);
    }
}
