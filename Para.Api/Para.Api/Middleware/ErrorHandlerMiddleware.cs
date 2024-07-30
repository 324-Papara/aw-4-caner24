using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Para.Api.Middleware;


public class ErrorHandlerMiddleware
{
    private readonly RequestDelegate next;

    public ErrorHandlerMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            // before controller invoke
            await next.Invoke(context);
            // after controller invoke
        }
        catch (Exception ex)
        {
            // log
            Log.Fatal(
                $"Path={context.Request.Path} || " +
                $"Method={context.Request.Method} || " +
                $"Exception={ex.Message}"
            );

            context.Response.StatusCode = 500;
            context.Request.ContentType = "application/json";

            var exceptionDetail = new ProblemDetails
            {
                Detail = ex.Message,
                Type = ex.GetType().ToString(),
                Status = context.Response.StatusCode
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(exceptionDetail));
        }

    }

}