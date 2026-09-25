using Bubox.Application.Interfaces;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;
using Bubox.Infrastructure.Repositories;
using Bubox.Infrastructure.Security;
using Bubox.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bubox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IGeneralRepository, GeneralRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IEmailSender, EmailSender>();

        return services;
    }
}
