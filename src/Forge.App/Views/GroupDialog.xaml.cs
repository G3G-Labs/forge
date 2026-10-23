using System.Windows;
using System.Windows.Input;
using Forge.App.ViewModels;

namespace Forge.App.Views;

public partial class GroupDialog : Window
{
    public GroupDialog(GroupEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, save) => DialogResult = save;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => DialogResult = false;
}
