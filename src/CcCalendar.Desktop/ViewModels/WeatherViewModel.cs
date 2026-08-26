using CcCalendar.Core.Configuration;
using CcCalendar.Core.Weather;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CcCalendar.Desktop.ViewModels;

public sealed class WeatherViewModel : ObservableObject
{
    private readonly IAutomaticLocationProvider automaticLocationProvider;
    private readonly Action<WeatherLocationSettings> settingsChanged;
    private readonly IWeatherService weatherService;
    private WeatherLocationSettings settings;
    private WeatherForecast? forecast;
    private bool isLoading;
    private bool isSearchResultsOpen;
    private IReadOnlyList<WeatherLocation> searchResults = [];
    private string searchQuery = string.Empty;
    private WeatherLocation? selectedSearchResult;
    private string statusMessage = string.Empty;

    public WeatherViewModel(
        IWeatherService weatherService,
        IAutomaticLocationProvider automaticLocationProvider,
        WeatherLocationSettings settings,
        Action<WeatherLocationSettings>? settingsChanged = null)
    {
        this.weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        this.automaticLocationProvider = automaticLocationProvider
            ?? throw new ArgumentNullException(nameof(automaticLocationProvider));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.settingsChanged = settingsChanged ?? (_ => { });
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(CancellationToken.None));
        SearchCommand = new AsyncRelayCommand(
            () => SearchAsync(SearchQuery, CancellationToken.None),
            () => !string.IsNullOrWhiteSpace(SearchQuery));
        SelectManualLocationCommand = new AsyncRelayCommand(
            () => SelectManualLocationAsync(SelectedSearchResult!, CancellationToken.None),
            () => SelectedSearchResult is not null);
    }

    public bool IsAutomaticLocation
    {
        get => settings.UseAutomaticLocation;
        set
        {
            if (value == settings.UseAutomaticLocation)
            {
                return;
            }

            settings = settings with { UseAutomaticLocation = value };
            settingsChanged(settings);
            OnPropertyChanged();
        }
    }

    public WeatherForecast? Forecast
    {
        get => forecast;
        private set
        {
            if (SetProperty(ref forecast, value))
            {
                OnPropertyChanged(nameof(LocationName));
                OnPropertyChanged(nameof(CurrentSummary));
                OnPropertyChanged(nameof(CurrentDetails));
                OnPropertyChanged(nameof(Hourly));
                OnPropertyChanged(nameof(Daily));
                OnPropertyChanged(nameof(HasForecast));
            }
        }
    }

    public IReadOnlyList<WeatherLocation> SearchResults
    {
        get => searchResults;
        private set => SetProperty(ref searchResults, value);
    }

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (SetProperty(ref searchQuery, value))
            {
                SearchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public WeatherLocation? SelectedSearchResult
    {
        get => selectedSearchResult;
        set
        {
            if (SetProperty(ref selectedSearchResult, value))
            {
                SelectManualLocationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public bool HasForecast => Forecast is not null;

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool IsSearchResultsOpen
    {
        get => isSearchResultsOpen;
        set => SetProperty(ref isSearchResultsOpen, value);
    }

    public string LocationName => Forecast?.Location.DisplayName ?? "未设置城市";

    public string CurrentSummary => Forecast is null
        ? "暂无天气"
        : $"{Forecast.Current.TemperatureCelsius:F0}° · {Describe(Forecast.Current.WeatherCode)}";

    public string CurrentDetails => Forecast is null
        ? string.Empty
        : $"体感 {Forecast.Current.ApparentTemperatureCelsius:F0}°  风速 {Forecast.Current.WindSpeedKilometersPerHour:F0} km/h";

    public IReadOnlyList<HourlyWeatherViewModel> Hourly
    {
        get
        {
            if (Forecast is null)
            {
                return [];
            }

            DateTime currentHour = new(
                Forecast.Current.ObservedAt.Year,
                Forecast.Current.ObservedAt.Month,
                Forecast.Current.ObservedAt.Day,
                Forecast.Current.ObservedAt.Hour,
                0,
                0,
                Forecast.Current.ObservedAt.Kind);
            DateTime previousHour = currentHour.AddHours(-1);
            DateTime firstFutureHour = currentHour.AddHours(1);
            DateTime endExclusive = currentHour.AddHours(25);
            List<HourlyWeatherViewModel> items = [.. Forecast.Hourly
                .Where(item => item.Time == previousHour
                    || (item.Time >= firstFutureHour && item.Time < endExclusive))
                .OrderBy(item => item.Time)
                .Select(item => new HourlyWeatherViewModel(
                    item.Time,
                    $"{item.Time:HH:mm}",
                    $"{item.TemperatureCelsius:F0}°",
                    Describe(item.WeatherCode),
                    $"降水 {item.PrecipitationProbabilityPercent}%"))];
            int previousIndex = items.FindIndex(item => item.Time == previousHour);
            items.Insert(
                previousIndex + 1,
                new HourlyWeatherViewModel(
                    Forecast.Current.ObservedAt,
                    "现在",
                    $"{Forecast.Current.TemperatureCelsius:F0}°",
                    Describe(Forecast.Current.WeatherCode),
                    "实时"));
            return items;
        }
    }

    public IReadOnlyList<DailyWeatherViewModel> Daily => Forecast?.Daily
        .Take(7)
        .Select(item => new DailyWeatherViewModel(
            item.Date,
            Describe(item.WeatherCode),
            $"{item.MaximumTemperatureCelsius:F0}° / {item.MinimumTemperatureCelsius:F0}°"))
        .ToArray() ?? [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand SearchCommand { get; }

    public IAsyncRelayCommand SelectManualLocationCommand { get; }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            WeatherLocation? location = null;
            string fallbackMessage = string.Empty;
            if (settings.UseAutomaticLocation)
            {
                try
                {
                    location = await automaticLocationProvider.GetCurrentLocationAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    fallbackMessage = "自动定位失败，已使用手动城市。";
                }
            }

            location ??= settings.ManualLocation?.ToWeatherLocation();
            if (location is null)
            {
                Forecast = null;
                StatusMessage = settings.UseAutomaticLocation
                    ? "自动定位不可用，请搜索并选择城市。"
                    : "请搜索并选择城市。";
                return;
            }

            Forecast = await weatherService.GetForecastAsync(location, cancellationToken);
            StatusMessage = fallbackMessage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = $"天气加载失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken)
    {
        IsLoading = true;
        IsSearchResultsOpen = false;
        try
        {
            SearchResults = string.IsNullOrWhiteSpace(query)
                ? []
                : await weatherService.SearchLocationsAsync(query, cancellationToken);
            IsSearchResultsOpen = SearchResults.Count > 0;
            StatusMessage = SearchResults.Count == 0 ? "未找到匹配城市。" : string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = $"城市搜索失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectManualLocationAsync(
        WeatherLocation location,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        settings = settings with
        {
            UseAutomaticLocation = false,
            ManualLocation = SavedWeatherLocation.From(location),
        };
        settingsChanged(settings);
        OnPropertyChanged(nameof(IsAutomaticLocation));
        await RefreshAsync(cancellationToken);
        if (Forecast is not null)
        {
            IsSearchResultsOpen = false;
            SearchResults = [];
            SearchQuery = string.Empty;
            SelectedSearchResult = null;
        }
    }

    private static string Describe(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "晴",
            1 or 2 => "多云",
            3 => "阴",
            45 or 48 => "雾",
            >= 51 and <= 57 => "毛毛雨",
            >= 61 and <= 67 => "雨",
            >= 71 and <= 77 => "雪",
            >= 80 and <= 82 => "阵雨",
            >= 85 and <= 86 => "阵雪",
            >= 95 and <= 99 => "雷雨",
            _ => "未知",
        };
    }
}

public sealed record HourlyWeatherViewModel(
    DateTime Time,
    string TimeLabel,
    string Temperature,
    string Condition,
    string Precipitation);

public sealed record DailyWeatherViewModel(
    DateOnly Date,
    string Condition,
    string TemperatureRange);
