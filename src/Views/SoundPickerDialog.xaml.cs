// Copyright © 2025 HemSoft

namespace TickDown.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TickDown.Core.Services;

/// <summary>
/// Dialog for selecting and previewing alarm sounds.
/// </summary>
public sealed partial class SoundPickerDialog : ContentDialog
{
    private readonly IAudioService audioService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundPickerDialog"/> class.
    /// </summary>
    public SoundPickerDialog()
    {
        this.InitializeComponent();
        this.audioService = App.Services.GetRequiredService<IAudioService>();
        this.AvailableSounds = this.audioService.AvailableSounds;
    }

    /// <summary>
    /// Gets the list of available sounds.
    /// </summary>
    public IReadOnlyList<string> AvailableSounds { get; }

    /// <summary>
    /// Gets or sets the selected sound.
    /// </summary>
    public string SelectedSound { get; set; } = "Alarm 01";

    private void OnPreviewClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(this.SelectedSound))
        {
            this.audioService.PlaySound(this.SelectedSound);
        }
    }
}