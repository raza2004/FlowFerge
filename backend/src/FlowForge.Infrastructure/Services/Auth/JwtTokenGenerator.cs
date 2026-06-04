using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FlowForge.Infrastructure.Services.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;
    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public string GenerateAccessToken(User user, Membership? membership)
    {
        var (token, _) = GenerateAccessTokenWithExpiry(user, membership);
        return token;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessTokenWithExpiry(User user, Membership? membership)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
        var issuer = _config["Jwt:Issuer"] ?? "FlowForge";
        var audience = _config["Jwt:Audience"] ?? "FlowForgeUsers";
        var minutes = int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15");

        var expiresAt = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("isSystemAdmin", user.IsSystemAdmin.ToString().ToLower())
        };

        if (membership != null)
        {
            claims.Add(new Claim("tenantId", membership.TenantId.ToString()));
            claims.Add(new Claim("role", membership.Role.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
