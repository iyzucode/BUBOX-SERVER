using Bubox.Domain.Entities;

namespace Bubox.Application.Interfaces;

public interface IJwtProvider
{
    string GenerateToken(User user, IEnumerable<string> roles);
}
