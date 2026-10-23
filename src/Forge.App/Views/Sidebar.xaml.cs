using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Forge.App.ViewModels;
using Forge.Core.Models;

namespace Forge.App.Views;

/// <summary>
/// Árvore de grupos e abas. O código daqui é só interação que o XAML não expressa: renomear no lugar,
/// arrastar abas entre grupos, o "+" que escolhe o tipo da aba e a configuração do grupo com duplo clique.
/// </summary>
public partial class Sidebar : UserControl
{
    private Point _dragStart;

    public Sidebar() => InitializeComponent();

    private MainWindowViewModel? Main => DataContext as MainWindowViewModel;

    // Renomear: o campo aparece já com o texto selecionado; Enter ou sair do campo confirma, Esc desiste.

    private void OnRenameVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && box.IsVisible)
        {
            box.Text = (box.DataContext as GroupViewModel)?.Name ?? (box.DataContext as TabViewModel)?.Name ?? box.Text;
            Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
    }

    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not IRenamable item)
            return;
        if (e.Key == Key.Enter)
        {
            Commit(box);
            item.IsRenaming = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            item.IsRenaming = false;
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is IRenamable { IsRenaming: true } item)
        {
            Commit(box);
            item.IsRenaming = false;
        }
    }

    private static void Commit(TextBox box)
    {
        if (!string.IsNullOrWhiteSpace(box.Text))
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void OnTabDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TabViewModel tab })
        {
            tab.IsRenaming = true;
            e.Handled = true;
        }
    }

    // Duplo clique no cabeçalho do grupo abre "Configurar grupo".
    private void OnGroupHeadClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: GroupViewModel group } && !group.IsRenaming)
        {
            group.ConfigureCommand.Execute(null);
            e.Handled = true;
        }
    }

    // "+" do grupo: escolher o ambiente e o terminal da aba nova. Os terminais do Windows ficam à vista; cada distro WSL
    // é um submenu com os shells do Linux e, abaixo, os terminais do Windows abertos na pasta da distro.
    private void OnNewTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GroupViewModel group } anchor || Main is not { } main)
            return;
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = $"Nova aba em {group.Name}", IsEnabled = false });
        AddShells(menu.Items, group, main.Shells.Where(s => !s.InWsl && s.Kind != ShellKind.Custom), "WINDOWS");

        var distros = main.Shells.Where(s => s.InWsl).GroupBy(s => s.Distro!).ToList();
        if (distros.Count > 0)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "WSL", IsEnabled = false, FontSize = 11 });
            foreach (var distro in distros)
            {
                var sub = new MenuItem { Header = distro.Key };
                foreach (var shell in distro.Where(s => s.IsWsl))
                    sub.Items.Add(new MenuItem { Header = shell.Terminal, Command = group.NewTabCommand, CommandParameter = shell });
                AddShells(sub.Items, group, distro.Where(s => !s.IsWsl), "TERMINAIS DO WINDOWS NA PASTA DA DISTRO");
                menu.Items.Add(sub);
            }
        }

        AddShells(menu.Items, group, main.Shells.Where(s => s.Kind == ShellKind.Custom), "PERFIS");
        menu.IsOpen = true;
    }

    private static void AddShells(ItemCollection items, GroupViewModel group, IEnumerable<ShellProfile> shells, string title)
    {
        var list = shells.ToList();
        if (list.Count == 0)
            return;
        items.Add(new Separator());
        items.Add(new MenuItem { Header = title, IsEnabled = false, FontSize = 11 });
        foreach (var shell in list)
            items.Add(new MenuItem { Header = shell.Distro is null ? shell.Name : shell.Terminal.Replace(" (Windows)", "", StringComparison.Ordinal), Command = group.NewTabCommand, CommandParameter = shell });
    }

    // Arrastar e soltar: reordena abas e move entre grupos. O processo da aba continua rodando.

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);

    private void OnTabMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { DataContext: TabViewModel tab } element || tab.IsRenaming)
            return;
        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        DragDrop.DoDragDrop(element, tab, DragDropEffects.Move);
    }

    private void OnTabDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TabViewModel)) is TabViewModel dragged && sender is FrameworkElement { DataContext: TabViewModel target }
            && dragged != target && Main is { } main)
        {
            main.MoveTab(dragged, target.Group, target.Group.Tabs.IndexOf(target));
            e.Handled = true;
        }
    }

    private void OnGroupDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TabViewModel)) is TabViewModel dragged && sender is FrameworkElement { DataContext: GroupViewModel group }
            && Main is { } main)
        {
            main.MoveTab(dragged, group, group.Tabs.Count);
            e.Handled = true;
        }
    }
}
