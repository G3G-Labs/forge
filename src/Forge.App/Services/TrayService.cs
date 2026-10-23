using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace Forge.App.Services;

/// <summary>
/// Ícone da bandeja. Fechar a janela (com "Fechar para a bandeja" ligado) só a esconde: os terminais e o que roda
/// neles continuam. Clique ou duplo clique no ícone traz a janela de volta; "Sair" encerra o Forge e os shells.
/// </summary>
public sealed class TrayService : IDisposable
{
    private TaskbarIcon? _icon;
    private bool _toldAboutTray;

    public event EventHandler? ShowWindowRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Abrir o Forge", () => ShowWindowRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Sair (fecha todos os terminais)", () => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _icon = new TaskbarIcon
        {
            ToolTipText = "Forge",
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = menu,
            MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        _icon.TrayLeftMouseUp += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>Na primeira vez que a janela vai para a bandeja, avisa que os terminais continuam rodando.</summary>
    public void NotifyHidden()
    {
        if (_toldAboutTray || _icon is null)
            return;
        _toldAboutTray = true;
        _icon.ShowNotification("O Forge continua aberto", "Os terminais seguem rodando. Clique no ícone para voltar; Sair no menu encerra tudo.");
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }
}
