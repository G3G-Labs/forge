using System.Text;
using System.Windows;
using System.Windows.Media;
using EasyWindowsTerminalControl;
using Forge.Core.ShellIntegration;
using Forge.Core.Shells;
using Microsoft.Terminal.Wpf;

namespace Forge.Terminal;

/// <summary>
/// O shell de uma aba: um controle do Windows Terminal (ConPTY + renderização nativa) e a leitura da integração
/// de shell na saída. Só existe para abas iniciadas; abas não iniciadas não têm processo nem controle.
/// Toda a dependência do EasyWindowsTerminalControl fica neste arquivo.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    private readonly OscParser _parser = new();
    private readonly TermPTY _pty;
    private readonly Lock _gate = new();
    private readonly List<string> _pendingInput = [];
    private bool _started;

    public TerminalSession(string tabId, LaunchPlan plan, TerminalTheme theme, string fontFamily, double fontSize)
    {
        TabId = tabId;
        Plan = plan;
        foreach (var script in plan.Scripts)
            WriteScript(script);

        _pty = new TermPTY();
        // A saída passa por aqui antes de ir para a tela: as OSC são lidas, nada é alterado.
        _pty.InterceptOutputToUITerminal = OnOutput;

        Control = new EasyTerminalControl
        {
            ConPTYTerm = _pty,
            StartupCommandLine = plan.CommandLine,
            FontFamilyWhenSettingTheme = new FontFamily(fontFamily),
            FontSizeWhenSettingTheme = fontSize,
            Visibility = Visibility.Collapsed,
        };
        // O tema vale depois da fonte: o controle aplica os dois juntos.
        Control.Theme = theme;
    }

    public string TabId { get; }

    public LaunchPlan Plan { get; }

    /// <summary>O controle nativo. A view o coloca na árvore visual e alterna a visibilidade.</summary>
    public EasyTerminalControl Control { get; }

    /// <summary>Primeira saída do processo: ele está vivo e lendo a entrada. Disparado na thread de leitura.</summary>
    public event EventHandler? Started;

    /// <summary>OSC 133/633/7 dos scripts. Disparado na thread de leitura: quem ouve volta para a da interface.</summary>
    public event EventHandler<ShellEvent>? ShellEventReceived;

    /// <summary>Digita no terminal. Antes de o processo responder, o texto espera numa fila.</summary>
    public void Send(string text)
    {
        lock (_gate)
        {
            if (!_started)
            {
                _pendingInput.Add(text);
                return;
            }
        }
        _pty.WriteToTerm(text.AsSpan());
    }

    /// <summary>Digita o comando e Enter.</summary>
    public void Run(string command) => Send(command + "\r");

    /// <summary>Ctrl+C: o ConPTY entrega como sinal ao processo em primeiro plano.</summary>
    public void Interrupt() => Send("\x03");

    public void ApplyTheme(TerminalTheme theme, string fontFamily, double fontSize)
    {
        Control.FontFamilyWhenSettingTheme = new FontFamily(fontFamily);
        Control.FontSizeWhenSettingTheme = fontSize;
        Control.Theme = theme;
    }

    public void Dispose()
    {
        // Fechar a entrada e o pseudoconsole encerra o shell; os filhos dele saem junto pelo Job Object do app.
        try { _pty.CloseStdinToApp(); } catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { /* já fechado */ }
        try { _pty.StopExternalTermOnly(); } catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { /* já fechado */ }
    }

    private void OnOutput(ref Span<char> text)
    {
        string[]? flush = null;
        var first = false;
        lock (_gate)
        {
            if (!_started)
            {
                _started = first = true;
                flush = [.. _pendingInput];
                _pendingInput.Clear();
            }
        }
        if (first)
            Started?.Invoke(this, EventArgs.Empty);

        foreach (var e in _parser.Feed(text))
            ShellEventReceived?.Invoke(this, e);

        if (flush is not null)
            foreach (var input in flush)
                _pty.WriteToTerm(input.AsSpan());
    }

    private static void WriteScript(ScriptFile script)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(script.WindowsPath)!);
        File.WriteAllText(script.WindowsPath, script.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: script.Utf8Bom));
    }
}
