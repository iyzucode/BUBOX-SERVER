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
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IGeneralService, GeneralService>();
        return services;
    }
}
