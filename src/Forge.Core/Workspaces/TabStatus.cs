using Forge.Core.ShellIntegration;

namespace Forge.Core.Workspaces;

/// <summary>Estado da aba na barra lateral: anel (parada), círculo verde (rodando), losango vermelho (erro).</summary>
public enum TabRunState
{
    /// <summary>Ainda sem shell: só uma linha na árvore, nenhum processo.</summary>
    NotStarted,

    /// <summary>Shell aberto, esperando comando.</summary>
    Idle,

    Running,

    /// <summary>O último comando terminou com código diferente de zero.</summary>
    Failed,

    /// <summary>O próprio shell foi encerrado (exit, processo caiu).</summary>
    Exited,
}

/// <summary>Um comando que terminou, para o histórico unificado.</summary>
public sealed record FinishedCommand(string Command, int? ExitCode, string? Cwd);

/// <summary>
/// Estado de uma aba montado a partir dos eventos do shell. Sem UI e sem relógio: a mesma sequência de eventos
/// sempre dá o mesmo estado, e os testes a reproduzem.
/// </summary>
public sealed class TabStatus
{
    // Ctrl+C: SIGINT no Linux (128 + 2) e STATUS_CONTROL_C_EXIT no Windows. Parar não é erro.
    private static readonly HashSet<int> InterruptCodes = [130, unchecked((int)0xC000013A)];

    private string? _pendingCommand;

    public TabRunState State { get; private set; } = TabRunState.NotStarted;

    public int? LastExitCode { get; private set; }

    public string? Cwd { get; private set; }

    /// <summary>O shell confirmou o prompt pelo menos uma vez: dá para enviar o comando de inicialização.</summary>
    public bool IsReady { get; private set; }

    public void ShellStarted()
    {
        State = TabRunState.Idle;
        IsReady = false;
        LastExitCode = null;
    }

    /// <summary>
    /// O app enviou um comando. Com integração, o <see cref="CommandStarted"/> confirma em seguida; sem ela (CMD),
    /// a aba fica como rodando até o usuário parar, porque não há como saber o fim.
    /// </summary>
    public void CommandSent() => State = TabRunState.Running;

    /// <summary>Sem integração não há aviso de fim: depois do Ctrl+C enviado pelo app, a aba é dada como parada.</summary>
    public void AssumeStopped()
    {
        if (State == TabRunState.Running)
            State = TabRunState.Idle;
    }

    public void ShellExited(int? exitCode)
    {
        State = TabRunState.Exited;
        LastExitCode = exitCode;
        IsReady = false;
    }

    /// <summary>Aplica um evento. Devolve o comando que terminou, quando houver, para ir ao histórico.</summary>
    public FinishedCommand? Apply(ShellEvent e)
    {
        switch (e)
        {
            case PromptStarted:
                IsReady = true;
                if (State is TabRunState.NotStarted or TabRunState.Exited)
                    State = TabRunState.Idle;
                return null;

            case CommandLineReceived line:
                _pendingCommand = line.Command;
                return null;

            case CommandStarted:
                State = TabRunState.Running;
                return null;

            case CommandFinished done:
                LastExitCode = done.ExitCode;
                State = done.ExitCode is null or 0 || InterruptCodes.Contains(done.ExitCode.Value)
                    ? TabRunState.Idle
                    : TabRunState.Failed;
                var command = _pendingCommand;
                _pendingCommand = null;
                return string.IsNullOrWhiteSpace(command) ? null : new FinishedCommand(command, done.ExitCode, Cwd);

            case CwdChanged cwd:
                Cwd = cwd.Path;
                return null;

            default:
                return null;
        }
    }
}
