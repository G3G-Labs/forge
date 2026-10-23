using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Forge.App.ViewModels;

namespace Forge.App.Views;

public partial class HistoryPopup : UserControl
{
    public HistoryPopup() => InitializeComponent();

    /// <summary>O popup abriu: o cursor vai direto para a busca.</summary>
    public void FocusSearch() => Dispatcher.BeginInvoke(() =>
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }, DispatcherPriority.Input);

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is HistorySearchViewModel vm)
            vm.PasteCommand.Execute(null);
    }
}
