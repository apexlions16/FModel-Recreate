using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FModel.Localization;

namespace FModel.Views;

public sealed class FirstRunWizard : Window
{
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _languageLabel;
    private readonly Button _continueButton;

    public FirstRunWizard()
    {
        Title = Constants.APP_NAME;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _title = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _description = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };
        _languageLabel = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };

        var languageBox = new ComboBox
        {
            ItemsSource = LocalizationManager.AvailableLanguages,
            DisplayMemberPath = nameof(LanguageOption.DisplayName),
            SelectedValuePath = nameof(LanguageOption.CultureName),
            SelectedValue = LocalizationManager.CurrentCultureName,
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 24)
        };

        _continueButton = new Button
        {
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        languageBox.SelectionChanged += (_, _) =>
        {
            if (languageBox.SelectedItem is not LanguageOption option) return;
            LocalizationManager.SetLanguage(option.CultureName);
            RefreshText();
        };
        _continueButton.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            File.WriteAllText(AppPaths.FirstRunMarker, DateTimeOffset.UtcNow.ToString("O"));
            DialogResult = true;
        };

        Content = new StackPanel
        {
            Margin = new Thickness(32),
            Children = { _title, _description, _languageLabel, languageBox, _continueButton }
        };

        RefreshText();
    }

    private void RefreshText()
    {
        _title.Text = LocalizationManager.Get("Welcome to FModel-Recreate");
        _description.Text = LocalizationManager.Get("Choose the interface language. The game directory selector will open next.");
        _languageLabel.Text = LocalizationManager.Get("Interface Language");
        _continueButton.Content = LocalizationManager.Get("Start");
        LocalizationManager.Apply(this);
    }
}
