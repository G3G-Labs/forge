using System.Windows;
using System.Windows.Input;
using Forge.App.ViewModels;

namespace Forge.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();
}
