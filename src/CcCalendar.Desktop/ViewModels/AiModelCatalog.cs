using System.Collections.ObjectModel;
using CcCalendar.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CcCalendar.Desktop.ViewModels;

public sealed class AiModelCatalog : ObservableObject
{
    private readonly AiProviderSettings fallbackSettings;
    private readonly Action<string?> selectionChanged;
    private AiModelConfiguration? selectedModel;

    public AiModelCatalog(
        IEnumerable<AiModelConfiguration> models,
        string? selectedModelId,
        AiProviderSettings fallbackSettings,
        Action<string?>? selectionChanged = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        this.fallbackSettings = fallbackSettings
            ?? throw new ArgumentNullException(nameof(fallbackSettings));
        this.selectionChanged = selectionChanged ?? (_ => { });
        Models = new ObservableCollection<AiModelConfiguration>(models);
        selectedModel = Models.FirstOrDefault(model => model.Id == selectedModelId)
            ?? Models.FirstOrDefault();
    }

    public ObservableCollection<AiModelConfiguration> Models { get; }

    public AiModelConfiguration? SelectedModel
    {
        get => selectedModel;
        set
        {
            if (value is not null && !Models.Any(model => model.Id == value.Id))
            {
                throw new ArgumentException("模型配置不在当前目录中。", nameof(value));
            }

            if (SetProperty(ref selectedModel, value))
            {
                OnPropertyChanged(nameof(CurrentSettings));
                selectionChanged(value?.Id);
            }
        }
    }

    public AiProviderSettings CurrentSettings => SelectedModel?.Settings ?? fallbackSettings;

    public void Upsert(AiModelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        int index = Models
            .Select((model, modelIndex) => (model, modelIndex))
            .Where(item => item.model.Id == configuration.Id)
            .Select(item => item.modelIndex)
            .DefaultIfEmpty(-1)
            .First();
        if (index < 0)
        {
            Models.Add(configuration);
        }
        else
        {
            Models[index] = configuration;
        }

        if (!ReferenceEquals(selectedModel, configuration))
        {
            selectedModel = configuration;
            OnPropertyChanged(nameof(SelectedModel));
            OnPropertyChanged(nameof(CurrentSettings));
            selectionChanged(configuration.Id);
        }
    }

    public void Remove(AiModelConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        int index = Models.IndexOf(configuration);
        if (index < 0)
        {
            return;
        }

        bool wasSelected = SelectedModel?.Id == configuration.Id;
        Models.RemoveAt(index);
        if (wasSelected)
        {
            SelectedModel = Models.FirstOrDefault();
        }
    }
}
