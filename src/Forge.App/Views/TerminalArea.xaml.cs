using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forge.App.Services;
using Forge.App.ViewModels;
using Forge.Terminal;

namespace Forge.App.Views;

/// <summary>
/// Os terminais nativos: um controle por aba iniciada, todos no mesmo lugar, e só o da aba selecionada visível.
/// Trocar de aba ou recolher um grupo não fecha nada: os processos continuam rodando por trás.
/// </summary>
public partial class TerminalArea : UserControl
{
    private MainWindowViewModel? _vm;
    private SessionManager? _sessions;

    public TerminalArea()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as MainWindowViewModel);
    }

    private void Attach(MainWindowViewModel? vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelChanged;
        if (_sessions is not null)
        {
            _sessions.Added -= OnSessionAdded;
            _sessions.Removed -= OnSessionRemoved;
        }

        _vm = vm;
        _sessions = vm?.Context.Sessions;
        if (vm is null || _sessions is null)
            return;

        vm.PropertyChanged += OnViewModelChanged;
        _sessions.Added += OnSessionAdded;
        _sessions.Removed += OnSessionRemoved;
        foreach (var session in _sessions.All)
            Add(session);
        ShowCurrent();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
            ShowCurrent();
    }

    private void OnSessionAdded(object? sender, TerminalSession session)
    {
        Add(session);
        ShowCurrent();
    }

    private void OnSessionRemoved(object? sender, TerminalSession session)
    {
        TermHost.Children.Remove(session.Control);
        ShowCurrent();
    }

    private void Add(TerminalSession session)
    {
        if (!TermHost.Children.Contains(session.Control))
            TermHost.Children.Add(session.Control);
    }

    private void ShowCurrent()
    {
        if (_sessions is null)
            return;
        var current = _vm?.SelectedTab?.Id;
        TerminalSession? visible = null;
        foreach (var session in _sessions.All)
        {
            var show = session.TabId == current;
            session.Control.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
                visible = session;
        }
        EmptyText.Visibility = visible is null ? Visibility.Visible : Visibility.Collapsed;
        if (visible is not null)
            Dispatcher.BeginInvoke(() => visible.Control.Focus(), DispatcherPriority.Input);
    }
}
