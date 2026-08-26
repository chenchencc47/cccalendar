using CcCalendar.Core.Workspaces;
using Microsoft.Extensions.Configuration;

namespace CcCalendar.Server;

/// <summary>
/// 局域网原型的开发令牌配置。仅供内网测试使用：生产环境必须关闭并改用 OIDC。
/// </summary>
public sealed record DevTokenOptions(
    bool Enabled,
    string SigningKey,
    string Issuer,
    string Audience,
    Guid? WorkspaceId,
    WorkspaceRole Role,
    int AccessTokenLifetimeMinutes,
    string? ConfiguredAuthority,
    string SharedSecret)
{
    public static DevTokenOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        bool enabled = bool.TryParse(
            configuration["Authentication:DevToken:Enabled"],
            out bool parsedEnabled)
            && parsedEnabled;
        string signingKey = configuration["Authentication:DevToken:SigningKey"]?.Trim() ?? string.Empty;
        string issuer = configuration["Authentication:DevToken:Issuer"]?.Trim() ?? string.Empty;
        if (issuer.Length == 0)
        {
            issuer = "cccalendar-dev";
        }

        string audience = configuration["Authentication:DevToken:Audience"]?.Trim() ?? string.Empty;
        if (audience.Length == 0)
        {
            audience = "cccalendar-api";
        }

        Guid? workspaceId = Guid.TryParse(
            configuration["Authentication:DevToken:WorkspaceId"],
            out Guid parsedWorkspaceId)
            ? parsedWorkspaceId
            : null;
        WorkspaceRole role = WorkspaceRole.Admin;
        string? roleValue = configuration["Authentication:DevToken:Role"];
        if (!string.IsNullOrWhiteSpace(roleValue)
            && !Enum.TryParse(roleValue, ignoreCase: true, out role))
        {
            throw new InvalidOperationException(
                $"Unsupported dev token role '{roleValue}'. Use Viewer, Member, Admin or Owner.");
        }

        int lifetimeMinutes = int.TryParse(
            configuration["Authentication:DevToken:AccessTokenLifetimeMinutes"],
            out int parsedLifetime)
            ? parsedLifetime
            : 480;
        string sharedSecret = configuration["Authentication:DevToken:SharedSecret"]?.Trim() ?? string.Empty;
        return new DevTokenOptions(
            enabled,
            signingKey,
            issuer,
            audience,
            workspaceId,
            role,
            lifetimeMinutes,
            configuration["Authentication:Authority"]?.Trim(),
            sharedSecret);
    }

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ConfiguredAuthority))
        {
            throw new InvalidOperationException(
                "Dev token mode cannot be combined with Authentication:Authority. Use OIDC or dev token, not both.");
        }

        if (SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Authentication:DevToken:SigningKey must be at least 32 characters when dev token mode is enabled.");
        }

        if (WorkspaceId is null || WorkspaceId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Authentication:DevToken:WorkspaceId is required when dev token mode is enabled.");
        }

        if (SharedSecret.Length is > 0 and < 8)
        {
            throw new InvalidOperationException(
                "Authentication:DevToken:SharedSecret must be at least 8 characters when configured.");
        }
    }
}
