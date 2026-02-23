namespace DenoLite.Web.Infrastructure;

/// <summary>
/// Adds Content-Security-Policy header to reduce XSS impact.
/// Inline scripts/styles in Razor pages require 'unsafe-inline'; for stricter CSP use nonces.
/// </summary>
public static class ContentSecurityPolicyMiddleware
{
    private const string HeaderName = "Content-Security-Policy";

    // Allows same-origin + inline scripts/styles (needed for Razor inline scripts and style attributes)
    private static readonly string Policy = string.Join("; ", new[]
    {
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data: https:",
        "connect-src 'self'",
        "form-action 'self'",
        "base-uri 'self'",
        "frame-ancestors 'self'",
        "font-src 'self'"
    });

    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app)
    {
        app.Use((context, next) =>
        {
            context.Response.Headers.Append(HeaderName, Policy);
            return next();
        });
        return app;
    }
}
