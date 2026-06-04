using FlowForge.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.API.Common;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess) return new OkObjectResult(result.Value);
        return MapError(result.Error);
    }

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess) return new NoContentResult();
        return MapError(result.Error);
    }

    private static IActionResult MapError(Error error)
    {
        var problem = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = error.Type switch
            {
                ErrorType.Validation => 400,
                ErrorType.NotFound => 404,
                ErrorType.Conflict => 409,
                ErrorType.Unauthorized => 401,
                ErrorType.Forbidden => 403,
                _ => 500
            }
        };
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
