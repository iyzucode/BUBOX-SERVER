using System.Text.Json;
using Bubox.Application.Exceptions;

namespace Bubox.Api.Middlewares;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, errorCode, message) = exception switch
        {
            EmailNotVerifiedException ex => (StatusCodes.Status403Forbidden, "EMAIL_NOT_VERIFIED", ex.Message),
            InvalidCredentialsException ex => (StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", ex.Message),
            UserAlreadyExistsException ex => (StatusCodes.Status409Conflict, "USER_ALREADY_EXISTS", ex.Message),
            UsernameAlreadyExistsException ex => (StatusCodes.Status409Conflict, "USERNAME_ALREADY_EXISTS", ex.Message),
            InvalidUsernameException ex => (StatusCodes.Status400BadRequest, "INVALID_USERNAME", ex.Message),
            InvalidOtpException ex => (StatusCodes.Status400BadRequest, "INVALID_OTP", ex.Message),
            UserNotFoundException ex => (StatusCodes.Status404NotFound, "USER_NOT_FOUND", ex.Message),
            AddressNotFoundException ex => (StatusCodes.Status404NotFound, "ADDRESS_NOT_FOUND", ex.Message),
            CannotDeletePrimaryAddressException ex => (StatusCodes.Status400BadRequest, "CANNOT_DELETE_PRIMARY_ADDRESS", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "Terjadi kesalahan pada server.")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            statusCode,
            errorCode,
            message,
            detail = exception.Message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
