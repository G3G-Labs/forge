namespace Forge.App;

/// <summary>
/// Argumentos do Forge.exe: <c>--admin</c> abre a janela de administrador; <c>--add-tab &lt;dados&gt;</c> cria nela a aba
/// vinda da janela comum; <c>--minimized</c> é usado ao iniciar com o Windows.
/// </summary>
public sealed record StartupArgs(bool Admin, string? AddTab, bool Minimized)
{
    public static StartupArgs Parse(IReadOnlyList<string> args)
    {
        bool admin = false, minimized = false;
        string? addTab = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--admin":
                    admin = true;
                    break;
                case "--minimized":
                    minimized = true;
                    break;
                case "--add-tab" when i + 1 < args.Count:
                    addTab = args[++i];
                    break;
            }
        }
        return new StartupArgs(admin, addTab, minimized);
    }
}
