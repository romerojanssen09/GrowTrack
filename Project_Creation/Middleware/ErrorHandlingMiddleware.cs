using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Project_Creation.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred during request processing");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var errorDetails = new
            {
                Message = exception.Message,
                Source = exception.Source,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message,
                Path = context.Request.Path,
                Method = context.Request.Method,
                QueryString = context.Request.QueryString.ToString(),
                Time = DateTime.UtcNow
            };

            var errorHtml = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Application Error</title>
                <style>
                    body {{ font-family: Arial, sans-serif; padding: 20px; }}
                    .error-container {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 15px; border-radius: 5px; }}
                    .error-title {{ color: #721c24; }}
                    .error-details {{ margin-top: 20px; background-color: #f8f9fa; padding: 15px; border-radius: 5px; }}
                    .error-stack {{ font-family: monospace; white-space: pre-wrap; margin-top: 10px; padding: 10px; background-color: #f1f1f1; border-radius: 5px; }}
                </style>
            </head>
            <body>
                <div class='error-container'>
                    <h2 class='error-title'>Application Error</h2>
                    <p>An unexpected error occurred while processing your request.</p>
                </div>
                
                <div class='error-details'>
                    <h3>Error Details:</h3>
                    <p><strong>Message:</strong> {exception.Message}</p>
                    <p><strong>Source:</strong> {exception.Source}</p>
                    <p><strong>Path:</strong> {context.Request.Path}</p>
                    <p><strong>Time:</strong> {DateTime.UtcNow}</p>
                    
                    {(exception.InnerException != null ? $"<p><strong>Inner Exception:</strong> {exception.InnerException.Message}</p>" : "")}
                    
                    <h4>Stack Trace:</h4>
                    <div class='error-stack'>{exception.StackTrace}</div>
                </div>
            </body>
            </html>";

            await context.Response.WriteAsync(errorHtml);
        }
    }

    // Extension method to make it easy to add the middleware
    public static class ErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ErrorHandlingMiddleware>();
        }
    }
}
