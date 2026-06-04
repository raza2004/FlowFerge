using FlowForge.Domain.Identity;

namespace FlowForge.Application.Common.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, Membership? membership);
    (string Token, DateTime ExpiresAt) GenerateAccessTokenWithExpiry(User user, Membership? membership);
}
