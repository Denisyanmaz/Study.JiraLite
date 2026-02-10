using JiraLite.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JiraLite.Api.Filters
{
    public class HttpResponseExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            switch (context.Exception)
            {
                case ForbiddenException forbiddenEx:
                    context.Result = new ObjectResult(new { message = forbiddenEx.Message })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    context.ExceptionHandled = true;
                    break;

                case ConflictException conflictEx:
                    context.Result = new ObjectResult(new { message = conflictEx.Message })
                    {
                        StatusCode = StatusCodes.Status409Conflict
                    };
                    context.ExceptionHandled = true;
                    break;

                case KeyNotFoundException notFoundEx:
                    context.Result = new ObjectResult(new { message = notFoundEx.Message })
                    {
                        StatusCode = StatusCodes.Status404NotFound
                    };
                    context.ExceptionHandled = true;
                    break;
            }
        }
    }
}
