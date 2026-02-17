using System.Net.Http.Json;

namespace JiraLite.Web.Helpers;

public static class ApiErrorReader
{
    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }

    public static async Task<string> ReadFriendlyMessageAsync(HttpResponseMessage response)
    {
        // If API returned ProblemDetails, show its "detail"
        try
        {
            if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
                if (!string.IsNullOrWhiteSpace(problem?.Detail))
                    return problem!.Detail!;
                if (!string.IsNullOrWhiteSpace(problem?.Title))
                    return problem!.Title!;
            }
        }
        catch
        {
            // ignore parse errors; fallback below
        }

        // Fallback: short generic message (don’t dump raw JSON to user)
        return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }
}
