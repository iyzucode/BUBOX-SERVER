using Bubox.Application.Interfaces;
using Bubox.Domain.Interfaces;
using Bubox.Infrastructure.Data;
using Bubox.Infrastructure.Repositories;
using Bubox.Infrastructure.Security;
using Bubox.Infrastructure.Services;
using Bubox.Infrastructure.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bubox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IMenuCategoryRepository, MenuCategoryRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IMenuAssignmentRepository, MenuAssignmentRepository>();
        services.AddScoped<IGeneralRepository, GeneralRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();

        // Email: mode "Smtp" (Resend) atau "Log" (development, hanya tulis ke log)
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(emailSection);
        if (string.Equals(emailSection[nameof(EmailOptions.Mode)], EmailModes.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LogEmailSender>();
        }

        // Order & payment
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOrderStatusTransitionRepository, OrderStatusTransitionRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();
        services.AddScoped<IOrderUnitOfWorkFactory, OrderUnitOfWorkFactory>();

        // HttpClient tunggal yang berumur panjang; koneksi di-refresh berkala agar perubahan DNS terbaca
        services.AddSingleton<IPaymentGateway>(sp => new MidtransPaymentGateway(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = TimeSpan.FromSeconds(30)
            },
            configuration,
            sp.GetRequiredService<ILogger<MidtransPaymentGateway>>()));

        return services;
    }
}
