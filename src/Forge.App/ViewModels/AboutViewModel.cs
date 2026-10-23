using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Forge.App.Services;

namespace Forge.App.ViewModels;

/// <summary>Um contato da empresa na tela Sobre.</summary>
public sealed record CompanyLink(string Label, string Value, string? Url, bool CanCopy = false);

/// <summary>Configurações › Sobre: o Forge e a empresa criadora, G3G Labs.</summary>
public sealed partial class AboutViewModel(Notifier notifier)
{
    public string Product => "Forge";

    public string Tagline => "Terminal leve com workspaces persistentes";

    public string Version { get; } =
        typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    public string Platform => "Windows 10 e 11";

    public string Company => "G3G Labs";

    public string CompanyTagline => "Software house para sistemas sob medida, aplicativos e soluções com IA.";

    public IReadOnlyList<CompanyLink> Links { get; } =
    [
        new("Site", "www.g3glabs.com.br", "https://www.g3glabs.com.br/"),
        new("CNPJ", "63.109.611/0001-92", null, CanCopy: true),
        new("E-mail", "g3glabs@gmail.com", "mailto:g3glabs@gmail.com"),
        new("WhatsApp", "Conversar no WhatsApp", "https://wa.me/558499999238"),
        new("LinkedIn", "@g3glabs", "https://www.linkedin.com/company/g3g-labs"),
        new("Instagram", "@g3glabs", "https://www.instagram.com/g3glabs"),
    ];

    public string Copyright => "© 2026 G3G Labs. Funciona offline, sem conta e sem telemetria.";

    [RelayCommand]
    private void Open(CompanyLink link)
    {
        if (link.Url is { } url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
    }

    [RelayCommand]
    private void Copy(CompanyLink link)
    {
        Clipboard.SetText(link.Value);
        notifier.Show($"{link.Label} copiado", link.Value);
    }
}
