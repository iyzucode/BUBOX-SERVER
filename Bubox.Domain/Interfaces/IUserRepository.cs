using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdentifierAsync(string identifier);
    Task<Guid> CreateAsync(User user);
    Task UpdatePasswordAsync(Guid userId, string passwordHash);
    Task SetEmailVerifiedAsync(Guid userId);
    Task SaveOtpAsync(UserOtp otp);
    Task<UserOtp?> GetValidOtpAsync(Guid userId, string otpCode, string type);
    Task MarkOtpUsedAsync(Guid otpId);
    Task<IEnumerable<string>> GetUserRolesAsync(Guid userId);
    Task AddUserRoleAsync(Guid userId, string roleCode);
}
