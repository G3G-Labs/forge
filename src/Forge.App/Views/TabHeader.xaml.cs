using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Forge.App.Views;

public partial class TabHeader : UserControl
{
    public TabHeader() => InitializeComponent();

    // O menu "…" abre no clique, logo abaixo do botão.
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
