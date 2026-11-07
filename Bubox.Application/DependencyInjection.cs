using Bubox.Application.Interfaces;
using Bubox.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bubox.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IMenuCategoryService, MenuCategoryService>();
        services.AddScoped<IGeneralService, GeneralService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<OrderStatusEngine>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderAdminService, OrderAdminService>();
        services.AddScoped<IPaymentService, PaymentService>();
        return services;
    }
}
