using Forge.Core.Util;

namespace Forge.Core.Models;

/// <summary>O workspaces.json inteiro: grupos, abas e o layout da janela. Nunca a saída dos terminais.</summary>
public sealed class WorkspaceFile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Aba visível quando o app fechou. Na restauração, é a primeira a ganhar shell.</summary>
    public string? SelectedTabId { get; set; }

    public WindowLayout Window { get; set; } = new();

    public List<WorkspaceGroup> Groups { get; set; } = [];
}

public sealed class WorkspaceGroup
{
    public string Id { get; set; } = Ids.New("g");

    public string Name { get; set; } = "";

    public string Color { get; set; } = Palette.GroupColors[0];

    public bool Collapsed { get; set; }

    /// <summary>Pasta do projeto. Toda aba do grupo abre nela, a menos que a aba tenha uma pasta própria.</summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Ambiente do grupo: o perfil de shell que as abas usam por padrão ("pwsh" no Windows, "wsl:Ubuntu" no WSL).
    /// null usa o shell padrão das configurações.
    /// </summary>
    public string? Shell { get; set; }

    public List<WorkspaceTab> Tabs { get; set; } = [];
}

public sealed class WorkspaceTab
{
    public string Id { get; set; } = Ids.New("t");

    public string Name { get; set; } = "";

    /// <summary>Cor própria da aba; null usa a do grupo.</summary>
    public string? Color { get; set; }

    /// <summary>
    /// Perfil de shell próprio da aba ("pwsh", "cmd", "gitbash", "wsl:Ubuntu", "custom:…"). null segue o ambiente do
    /// grupo: mudar o ambiente do grupo muda todas as abas que não escolheram outro.
    /// </summary>
    public string? Shell { get; set; }

    /// <summary>Pasta própria da aba, no formato do shell (C:\dev\web, /home/dev/api ou ~/api). null usa a do grupo.</summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Comando ao abrir (opcional), escrito pelo usuário: bun dev, yarn dev, npm run dev… Roda sempre que o shell da
    /// aba abre. O Forge não adivinha ferramenta nem tem botão de iniciar ou parar projeto.
    /// </summary>
    public string? Startup { get; set; }

    /// <summary>Abre a aba (e roda o comando, se houver) junto com o app, mesmo sem estar visível.</summary>
    public bool Autostart { get; set; }

    /// <summary>Variáveis de ambiente opcionais, aplicadas pelo script que o app gera para a aba.</summary>
    public Dictionary<string, string> Env { get; set; } = new();

    /// <summary>Última pasta visitada. Só é usada com "Ao reabrir, voltar para a última pasta visitada" ligado.</summary>
    public string? LastCwd { get; set; }

    public WorkspaceTab CloneWithNewId() => new()
    {
        Name = Name,
        Color = Color,
        Shell = Shell,
        Cwd = Cwd,
        Startup = Startup,
        Autostart = Autostart,
        Env = new Dictionary<string, string>(Env),
        LastCwd = LastCwd,
    };
}

/// <summary>Tamanho, posição e barra lateral da janela, para reabrir exatamente como estava.</summary>
public sealed class WindowLayout
{
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double Width { get; set; } = 1280;

    public double Height { get; set; } = 800;

    public bool Maximized { get; set; }

    public double SidebarWidth { get; set; } = 248;

    public bool SidebarCollapsed { get; set; }
}
