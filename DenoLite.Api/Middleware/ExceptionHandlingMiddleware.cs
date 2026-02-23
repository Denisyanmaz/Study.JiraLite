using DenoLite.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace DenoLite.Api.Middleware
{
    public sealed class ExceptionHandlingMiddleware : IMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (ForbiddenException ex)
            {
                await WriteProblem(context, StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteProblem(context, StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (BadRequestException ex)
            {
                await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (TooManyRequestsException ex)
            {
                await WriteProblem(context, StatusCodes.Status429TooManyRequests, ex.Message);
            }

            catch (NotFoundException ex)
            {
                await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message);
            }
            catch (ArgumentException ex)
            {
                await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (ConflictException ex) // ? ADD THIS
            {
                await WriteProblem(context, StatusCodes.Status409Conflict, ex.Message);
            }
            catch (ServiceUnavailableException ex)
            {
                await WriteProblem(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await WriteProblem(context, StatusCodes.Status409Conflict, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
                await WriteProblem(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred."
                );
            }
        }

        private static async Task WriteProblem(HttpContext context, int statusCode, string detail)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail = detail,
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }

    }
}
