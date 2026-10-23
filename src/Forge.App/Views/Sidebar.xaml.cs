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

    // "+" do grupo: escolher o tipo da aba nova. Windows primeiro, depois cada distro WSL e os perfis próprios.
    private void OnNewTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GroupViewModel group } anchor || Main is not { } main)
            return;
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = $"Nova aba em {group.Name}", IsEnabled = false });
        AddShells(menu, group, main.Shells.Where(s => s.Kind is not (ShellKind.Wsl or ShellKind.Custom)), "WINDOWS");
        AddShells(menu, group, main.Shells.Where(s => s.Kind == ShellKind.Wsl), "WSL");
        AddShells(menu, group, main.Shells.Where(s => s.Kind == ShellKind.Custom), "PERFIS");
        menu.IsOpen = true;
    }

    private static void AddShells(ContextMenu menu, GroupViewModel group, IEnumerable<ShellProfile> shells, string title)
    {
        var list = shells.ToList();
        if (list.Count == 0)
            return;
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = title, IsEnabled = false, FontSize = 11 });
        foreach (var shell in list)
            menu.Items.Add(new MenuItem { Header = shell.Name, Command = group.NewTabCommand, CommandParameter = shell });
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
