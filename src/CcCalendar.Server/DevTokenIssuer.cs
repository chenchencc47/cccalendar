using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CcCalendar.Server;

public sealed record DevTokenLoginRequest(string? Name, string? SharedSecret);

public sealed record DevTokenResponse(
    string AccessToken,
    string TokenType,
    Guid UserId,
    Guid WorkspaceId,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// 局域网原型的开发令牌签发：按姓名派生确定性用户 ID 并签发 HS256 JWT。
/// 仅供内网测试使用，生产环境必须关闭。
/// </summary>
public static class DevTokenIssuer
{
    private static readonly Guid NamespaceId = new("8f1c3a52-9d47-4b6e-a3f2-5c9d0e7b1a44");
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    public static Guid GetUserId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        byte[] hash = SHA256.HashData(
            NamespaceId.ToByteArray()
                .Concat(Encoding.UTF8.GetBytes(name.Trim()))
                .ToArray());
        Span<byte> identifier = hash.AsSpan(0, 16);
        identifier[6] = (byte)((identifier[6] & 0x0F) | 0x50);
        identifier[8] = (byte)((identifier[8] & 0x3F) | 0x80);
        return new Guid(identifier);
    }

    public static string IssueAccessToken(
        DevTokenOptions options,
        Guid userId,
        string name,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = TokenHandler.CreateJwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            subject: new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, name.Trim()),
            ]),
            notBefore: now,
            issuedAt: now,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);
        return TokenHandler.WriteToken(token);
    }
}
