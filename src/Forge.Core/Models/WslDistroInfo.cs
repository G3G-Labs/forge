namespace Forge.Core.Models;

/// <summary>
/// O que o Forge sabe de uma distro WSL, lido dentro dela (/etc/shells e o usuário padrão): a home, o shell de login e
/// os shells instalados. Fica no settings.json para não precisar ligar a distro toda vez que o app abre.
/// </summary>
public sealed record WslDistroInfo(string? Home, string DefaultShell, IReadOnlyList<string> Shells);
