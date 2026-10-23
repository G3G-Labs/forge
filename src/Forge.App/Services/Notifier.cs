using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Diagnostics;

namespace Forge.App.Services;

/// <summary>
/// Aviso discreto no canto da janela ("Workspaces restaurados", "Caminho copiado", erros). Todo erro mostrado também
/// vai para o log de auditoria.
/// </summary>
public sealed partial class Notifier : ObservableObject
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly AuditLog _audit;

    public Notifier(AuditLog audit)
    {
        _audit = audit;
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
        if (isError)
            _audit.Error("aviso", title, message);
        Display(title, message, isError);
    }

    /// <summary>Mostra a mensagem do erro e grava no log o erro completo, com a pilha.</summary>
    public void Error(Exception ex, string title = "Algo deu errado")
    {
        _audit.Error("erro", title, ex);
        Display(title, ex.Message, isError: true);
    }

    private void Display(string title, string message, bool isError)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Display(title, message, isError));
            return;
        }
        Title = title;
        Message = message;
        IsError = isError;
        IsVisible = true;
        _timer.Stop();
        _timer.Start();
    }

    [RelayCommand]
    private void Dismiss() => IsVisible = false;
}
