using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Forge.Core.Util;

namespace Forge.Terminal.Native;

/// <summary>
/// Abas de administrador. O Windows não eleva um processo filho dentro de uma janela comum, então elas vivem numa
/// segunda janela do Forge, elevada pelo UAC e marcada em amarelo.
/// </summary>
public static class Elevation
{
    private const int ErrorCancelled = 1223;

    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Abre (ou chama, se já estiver aberta) a janela de administrador, depois da confirmação do UAC.
    /// <paramref name="args"/> segue para ela (ex.: a aba a criar). Devolve false se o usuário recusou.
    /// </summary>
    public static bool StartAdminWindow(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(' ', args.Prepend("--admin").Select(CommandLine.Quote)),
        };
        try
        {
            Process.Start(psi)?.Dispose();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }
}
