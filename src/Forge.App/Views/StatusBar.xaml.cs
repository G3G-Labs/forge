using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forge.App.ViewModels;

namespace Forge.App.Views;

public partial class StatusBar : UserControl
{
    public StatusBar() => InitializeComponent();

    // Clique na branch: as branches locais, a atual marcada. Escolher troca pelo git, sem digitar no terminal.
    private void OnBranchClick(object sender, RoutedEventArgs e)
    {
        if (BranchButton.DataContext is TabViewModel tab)
            _ = ShowBranchesAsync(tab);
    }

    private async Task ShowBranchesAsync(TabViewModel tab)
    {
        var branches = await tab.BranchesAsync();
        var menu = new ContextMenu { PlacementTarget = BranchButton, Placement = PlacementMode.Top };
        if (branches.Count == 0)
            menu.Items.Add(new MenuItem { Header = "Nenhuma branch local encontrada", IsEnabled = false });
        foreach (var branch in branches)
        {
            menu.Items.Add(new MenuItem
            {
                Header = branch,
                IsCheckable = false,
                IsChecked = branch == tab.Branch,
                Command = tab.SwitchBranchCommand,
                CommandParameter = branch,
            });
        }
        menu.IsOpen = true;
    }
}
