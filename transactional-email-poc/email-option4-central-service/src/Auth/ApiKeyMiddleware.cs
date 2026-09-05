using System.Security.Cryptography;
using System.Text;

namespace EmailCentral.Api.Auth;

/// <summary>Static validator extracted from the middleware for unit testing.</summary>
public static class ApiKeyValidator
{
    public static bool IsValid(IConfiguration configuration, string sourceSystem, string presentedKey)
    {
        if (string.IsNullOrEmpty(sourceSystem) || string.IsNullOrEmpty(presentedKey))
        {
            return false;
        }

        var configuredKey = configuration[$"ApiKeys:{sourceSystem}"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presentedKey),
            Encoding.UTF8.GetBytes(configuredKey));
    }
}

/// <summary>
/// Per-system API-key middleware. Callers present X-Source-System + X-Api-Key.
/// Keys are configured per source system so the activity log can attribute every send.
/// /health, /openapi and the event webhook are anonymous in the POC.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string SourceSystemHeader = "X-Source-System";
    private const string ApiKeyHeader = "X-Api-Key";

    private static readonly string[] AnonymousPaths =
    [
        "/health",
        "/openapi",
        "/api/v1/events/sendgrid"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (AnonymousPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await next(context);
            return;
        }

        var sourceSystem = context.Request.Headers[SourceSystemHeader].ToString();
        var presentedKey = context.Request.Headers[ApiKeyHeader].ToString();

        if (!ApiKeyValidator.IsValid(configuration, sourceSystem, presentedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Provide valid X-Source-System and X-Api-Key headers."
            });
            return;
        }

        await next(context);
    }
}
