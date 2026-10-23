using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Forge.App.Services;

/// <summary>Aviso discreto no canto da janela ("Workspaces restaurados", "Caminho copiado", erros).</summary>
public sealed partial class Notifier : ObservableObject
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    public Notifier()
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            IsVisible = false;
        };
    }

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>Pode ser chamado de qualquer thread.</summary>
    public void Show(string title, string message = "", bool isError = false)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Show(title, message, isError));
            return;
        }
        Title = title;
        Message = message;
        IsError = isError;
        IsVisible = true;
        _timer.Stop();
        _timer.Start();
    }

    public void Error(Exception ex) => Show("Algo deu errado", ex.Message, isError: true);

    [RelayCommand]
    private void Dismiss() => IsVisible = false;
}
