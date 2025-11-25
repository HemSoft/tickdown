namespace TickDown.Views;

using Microsoft.Extensions.DependencyInjection;
using TickDown.ViewModels;

/// <summary>
/// A simple page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = ViewModel;
    }
}