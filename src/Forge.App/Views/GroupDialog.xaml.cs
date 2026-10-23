using System.Windows;
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
}
