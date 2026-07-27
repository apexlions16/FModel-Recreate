using System.Threading.Tasks;
using FModel.Framework;
using FModel.Localization;

namespace FModel.ViewModels;

public class AboutViewModel : ViewModel
{
    private string _descriptionLabel;
    public string DescriptionLabel
    {
        get => _descriptionLabel;
        set => SetProperty(ref _descriptionLabel, value);
    }

    private string _contributorsLabel;
    public string ContributorsLabel
    {
        get => _contributorsLabel;
        set => SetProperty(ref _contributorsLabel, value);
    }

    private string _referencesLabel;
    public string ReferencesLabel
    {
        get => _referencesLabel;
        set => SetProperty(ref _referencesLabel, value);
    }

    public Task Initialize()
    {
        DescriptionLabel = LocalizationManager.Get(
            "FModel-Recreate is an Unreal Engine archive explorer based on FModel and CUE4Parse. This release establishes an independent update channel, branding and localization foundation.");
        ContributorsLabel = LocalizationManager.Get(
            "Built by apexlions16. Based on the GPL-3.0 licensed FModel project by 4sval and its contributors.");
        ReferencesLabel = "FModel, CUE4Parse, Adonis UI, AvalonEdit, CSCore, EpicManifestParser, " +
                          "OpenTK, Newtonsoft.Json, Serilog, SkiaSharp, vgmstream";
        return Task.CompletedTask;
    }
}
