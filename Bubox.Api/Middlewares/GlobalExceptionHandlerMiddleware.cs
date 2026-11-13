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
            InvalidGoogleTokenException ex => (StatusCodes.Status401Unauthorized, "INVALID_GOOGLE_TOKEN", ex.Message),
            PasswordNotSetException ex => (StatusCodes.Status401Unauthorized, "PASSWORD_NOT_SET", ex.Message),
            OtpAttemptsExceededException ex => (StatusCodes.Status400BadRequest, "OTP_ATTEMPTS_EXCEEDED", ex.Message),
            UserNotFoundException ex => (StatusCodes.Status404NotFound, "USER_NOT_FOUND", ex.Message),
            InvalidAvatarException ex => (StatusCodes.Status400BadRequest, "INVALID_AVATAR", ex.Message),
            AvatarTooLargeException ex => (StatusCodes.Status400BadRequest, "AVATAR_TOO_LARGE", ex.Message),
            ProfileChangeCooldownException ex => (StatusCodes.Status400BadRequest, "CHANGE_COOLDOWN", ex.Message),
            InvalidFullNameException ex => (StatusCodes.Status400BadRequest, "INVALID_FULL_NAME", ex.Message),
            InvalidPhoneNumberException ex => (StatusCodes.Status400BadRequest, "INVALID_PHONE_NUMBER", ex.Message),
            InvalidCoordinatesException ex => (StatusCodes.Status400BadRequest, "INVALID_COORDINATES", ex.Message),
            LocationNotFoundException ex => (StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", ex.Message),
            GeocodingUnavailableException ex => (StatusCodes.Status503ServiceUnavailable, "GEOCODING_UNAVAILABLE", ex.Message),
            AddressNotFoundException ex => (StatusCodes.Status404NotFound, "ADDRESS_NOT_FOUND", ex.Message),
            CannotDeletePrimaryAddressException ex => (StatusCodes.Status400BadRequest, "CANNOT_DELETE_PRIMARY_ADDRESS", ex.Message),
            OrderNotFoundException ex => (StatusCodes.Status404NotFound, "ORDER_NOT_FOUND", ex.Message),
            InvalidOrderException ex => (StatusCodes.Status400BadRequest, "INVALID_ORDER", ex.Message),
            OrderCutoffPassedException ex => (StatusCodes.Status400BadRequest, "ORDER_CUTOFF_PASSED", ex.Message),
            InvalidStatusTransitionException ex => (StatusCodes.Status409Conflict, "INVALID_STATUS_TRANSITION", ex.Message),
            ForbiddenStatusTransitionException ex => (StatusCodes.Status403Forbidden, "FORBIDDEN_STATUS_TRANSITION", ex.Message),
            OrderConcurrencyException ex => (StatusCodes.Status409Conflict, "ORDER_CONCURRENCY_CONFLICT", ex.Message),
            PaymentGatewayException ex => (StatusCodes.Status502BadGateway, "PAYMENT_GATEWAY_ERROR", ex.Message),
            InvalidPaymentNotificationException ex => (StatusCodes.Status400BadRequest, "INVALID_PAYMENT_NOTIFICATION", ex.Message),
            InvalidSettingException ex => (StatusCodes.Status400BadRequest, "INVALID_SETTING", ex.Message),
            VoucherNotFoundException ex => (StatusCodes.Status404NotFound, "VOUCHER_NOT_FOUND", ex.Message),
            VoucherNotApplicableException ex => (StatusCodes.Status400BadRequest, "VOUCHER_NOT_APPLICABLE", ex.Message),
            InvalidVoucherException ex => (StatusCodes.Status400BadRequest, "INVALID_VOUCHER", ex.Message),
            VoucherCodeExistsException ex => (StatusCodes.Status409Conflict, "VOUCHER_CODE_EXISTS", ex.Message),
            DeliveryNotCoveredException ex => (StatusCodes.Status400BadRequest, "DELIVERY_NOT_COVERED", ex.Message),
            InvalidDeliverySettingException ex => (StatusCodes.Status400BadRequest, "INVALID_DELIVERY_SETTING", ex.Message),
            DeliveryZoneNotFoundException ex => (StatusCodes.Status404NotFound, "DELIVERY_ZONE_NOT_FOUND", ex.Message),
            DeliveryZoneCodeExistsException ex => (StatusCodes.Status409Conflict, "DELIVERY_ZONE_CODE_EXISTS", ex.Message),
            DeliveryZoneNotEmptyException ex => (StatusCodes.Status409Conflict, "DELIVERY_ZONE_NOT_EMPTY", ex.Message),
            DriverNotAssignedException ex => (StatusCodes.Status409Conflict, "DRIVER_NOT_ASSIGNED", ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", ex.Message),
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
