using System.Text.Json;
using CcCalendar.Core.Configuration;
using CcCalendar.Core.Security;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class TeamConnectionViewModel : ObservableObject
{
    private readonly Func<OidcClientOptions, CancellationToken, Task<OidcTokenSet>> authenticate;
    private readonly Func<Uri, string, string?, CancellationToken, Task<DevTokenLoginResult>>? devTokenLogin;
    private readonly OidcTokenStore tokenStore;
    private readonly Action<TeamConnectionSettings> settingsChanged;
    private TeamConnectionSettings settings;
    private bool isAuthenticated;
    private bool isBusy;
    private string statusMessage = string.Empty;

    public TeamConnectionViewModel(
        TeamConnectionSettings settings,
        Func<OidcClientOptions, CancellationToken, Task<OidcTokenSet>> authenticate,
        OidcTokenStore tokenStore,
        Action<TeamConnectionSettings> settingsChanged,
        Func<Uri, string, string?, CancellationToken, Task<DevTokenLoginResult>>? devTokenLogin = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.authenticate = authenticate ?? throw new ArgumentNullException(nameof(authenticate));
        this.tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        this.settingsChanged = settingsChanged ?? throw new ArgumentNullException(nameof(settingsChanged));
        this.devTokenLogin = devTokenLogin;
        LoginCommand = new AsyncRelayCommand(() => LoginAsync(CancellationToken.None), () => !IsBusy);
        LogoutCommand = new AsyncRelayCommand(() => LogoutAsync(CancellationToken.None), () => !IsBusy);
    }

    public AsyncRelayCommand LoginCommand { get; }

    public AsyncRelayCommand LogoutCommand { get; }

    public event EventHandler? ConnectionChanged;

    public bool IsAuthenticated
    {
        get => isAuthenticated;
        private set => SetProperty(ref isAuthenticated, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                LoginCommand.NotifyCanExecuteChanged();
                LogoutCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ApiBaseUrl
    {
        get => settings.ApiBaseUrl;
        set => UpdateSettings(settings with { ApiBaseUrl = value });
    }

    public string AuthorizationEndpoint
    {
        get => settings.AuthorizationEndpoint;
        set => UpdateSettings(settings with { AuthorizationEndpoint = value });
    }

    public string TokenEndpoint
    {
        get => settings.TokenEndpoint;
        set => UpdateSettings(settings with { TokenEndpoint = value });
    }

    public string ClientId
    {
        get => settings.ClientId;
        set => UpdateSettings(settings with { ClientId = value });
    }

    public string WorkspaceId
    {
        get => settings.WorkspaceId;
        set => UpdateSettings(settings with { WorkspaceId = value });
    }

    public string DevTokenName
    {
        get => settings.DevTokenName;
        set => UpdateSettings(settings with { DevTokenName = value });
    }

    public string DevTokenSharedSecret
    {
        get => settings.DevTokenSharedSecret;
        set => UpdateSettings(settings with { DevTokenSharedSecret = value });
    }

    public string CurrentUserId => settings.CurrentUserId;

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在打开登录页面…";
        try
        {
            if (IsDevTokenMode)
            {
                await LoginWithDevTokenAsync(cancellationToken);
            }
            else
            {
                OidcClientOptions options = CreateOidcOptions();
                OidcTokenSet tokens = await authenticate(options, cancellationToken);
                await tokenStore.WriteAsync(TokenIdentifier, tokens, cancellationToken);
                IsAuthenticated = true;
                StatusMessage = "已登录";
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException)
        {
            IsAuthenticated = false;
            StatusMessage = exception.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            IsAuthenticated = false;
            StatusMessage = "登录已取消";
        }
        catch (Exception exception)
        {
            IsAuthenticated = false;
            StatusMessage = $"登录失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>启动时恢复凭据；开发令牌过期时使用已保存的姓名和口令静默续签。</summary>
    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        OidcTokenSet? tokens = await tokenStore.ReadAsync(TokenIdentifier, cancellationToken);
        if (tokens is { } stored
            && !string.IsNullOrWhiteSpace(stored.AccessToken)
            && stored.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            if (IsDevTokenMode
                && TryReadGuidSubject(stored.AccessToken, out Guid subject)
                && !string.Equals(CurrentUserId, subject.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                UpdateSettings(settings with { CurrentUserId = subject.ToString() });
            }

            IsAuthenticated = true;
            StatusMessage = "已登录";
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsDevTokenMode && !string.IsNullOrWhiteSpace(DevTokenName))
        {
            await LoginAsync(cancellationToken);
            return;
        }

        IsAuthenticated = false;
        StatusMessage = string.Empty;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await tokenStore.DeleteAsync(TokenIdentifier, cancellationToken);
            IsAuthenticated = false;
            StatusMessage = "已退出登录";
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsDevTokenMode => string.IsNullOrWhiteSpace(AuthorizationEndpoint)
        || string.IsNullOrWhiteSpace(TokenEndpoint);

    private string TokenIdentifier => settings.ResolveTokenIdentifier();

    private async Task LoginWithDevTokenAsync(CancellationToken cancellationToken)
    {
        if (devTokenLogin is null)
        {
            throw new UriFormatException("当前版本未启用开发令牌登录。");
        }

        if (string.IsNullOrWhiteSpace(DevTokenName))
        {
            throw new ArgumentException("请先填写开发令牌姓名。");
        }

        if (!Uri.TryCreate(ApiBaseUrl?.Trim(), UriKind.Absolute, out Uri? apiBaseUrl)
            || string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            throw new UriFormatException("请先填写有效的服务端地址。");
        }

        StatusMessage = "正在登录…";
        DevTokenLoginResult result = await devTokenLogin(
            apiBaseUrl,
            DevTokenName.Trim(),
            string.IsNullOrWhiteSpace(DevTokenSharedSecret) ? null : DevTokenSharedSecret.Trim(),
            cancellationToken);
        await tokenStore.WriteAsync(TokenIdentifier, result.Tokens, cancellationToken);
        IsAuthenticated = true;
        StatusMessage = "已登录";
        TeamConnectionSettings updated = settings;
        if (result.WorkspaceId != Guid.Empty
            && !string.Equals(WorkspaceId, result.WorkspaceId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            updated = updated with { WorkspaceId = result.WorkspaceId.ToString() };
        }

        if (result.UserId != Guid.Empty
            && !string.Equals(CurrentUserId, result.UserId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            updated = updated with { CurrentUserId = result.UserId.ToString() };
        }

        if (!ReferenceEquals(updated, settings))
        {
            UpdateSettings(updated);
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private OidcClientOptions CreateOidcOptions()
    {
        if (!Uri.TryCreate(AuthorizationEndpoint, UriKind.Absolute, out Uri? authorizationEndpoint)
            || !Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out Uri? tokenEndpoint))
        {
            throw new UriFormatException("请先填写有效的 OIDC 授权和令牌地址。");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new ArgumentException("请先填写 OIDC 客户端 ID。");
        }

        return new OidcClientOptions(
            authorizationEndpoint,
            tokenEndpoint,
            ClientId.Trim(),
            new Uri("http://127.0.0.1/callback/"),
            ["openid", "profile", "offline_access"]);
    }

    private void UpdateSettings(TeamConnectionSettings updated)
    {
        if (Equals(settings, updated))
        {
            return;
        }

        settings = updated;
        settingsChanged(updated);
        OnPropertyChanged(string.Empty);
    }

    private static bool TryReadGuidSubject(string accessToken, out Guid subject)
    {
        subject = Guid.Empty;
        string[] segments = accessToken.Split('.');
        if (segments.Length < 2)
        {
            return false;
        }

        try
        {
            string payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using JsonDocument document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("sub", out JsonElement value)
                && Guid.TryParse(value.GetString(), out subject);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
