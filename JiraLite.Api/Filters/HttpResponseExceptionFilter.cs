using JiraLite.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JiraLite.Api.Filters
{
    public class HttpResponseExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is ForbiddenException forbiddenEx)
            {
                context.Result = new ObjectResult(new { message = forbiddenEx.Message })
                {
                    StatusCode = 403
                };
                context.ExceptionHandled = true;
            }

            // Optional: map other exceptions here
            if (context.Exception is KeyNotFoundException notFoundEx)
            {
                context.Result = new NotFoundObjectResult(new { message = notFoundEx.Message });
                context.ExceptionHandled = true;
            }
        }
    }
}
