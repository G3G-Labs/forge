namespace Forge.Core.ShellIntegration;

/// <summary>O que os scripts de integração contam pela saída do terminal (sequências OSC invisíveis).</summary>
public abstract record ShellEvent;

/// <summary>OSC 133;A — o prompt vai ser desenhado: o shell está pronto para receber comandos.</summary>
public sealed record PromptStarted : ShellEvent;

/// <summary>OSC 633;E — o texto do comando que o usuário acabou de enviar.</summary>
public sealed record CommandLineReceived(string Command) : ShellEvent;

/// <summary>OSC 133;C — o comando começou a rodar.</summary>
public sealed record CommandStarted : ShellEvent;

/// <summary>OSC 133;D — o comando terminou. Só é emitido se houve um <see cref="CommandStarted"/> antes.</summary>
public sealed record CommandFinished(int? ExitCode) : ShellEvent;

/// <summary>OSC 7 — a pasta atual mudou.</summary>
public sealed record CwdChanged(string Path) : ShellEvent;
