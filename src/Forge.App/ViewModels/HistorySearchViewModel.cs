using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forge.Core.Models;
using Forge.Core.Persistence;

namespace Forge.App.ViewModels;

/// <summary>Passos da seleção no Ctrl+R (parâmetro das teclas ↑ e ↓ no XAML).</summary>
public static class HistoryKeys
{
    public static readonly int Up = -1;
    public static readonly int Down = 1;
}

/// <summary>Uma linha do resultado do Ctrl+R.</summary>
public sealed record HistoryItem(HistoryEntry Entry)
{
    public string Command => Entry.Command;

    public string Where => $"{Entry.Group} / {Entry.TabName}";

    public string Cwd => Entry.Cwd ?? "";

    public bool Failed => Entry.ExitCode is not null and not 0;

    public string When
    {
        get
        {
            var at = Entry.At.ToLocalTime();
            var today = DateTimeOffset.Now.Date;
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            if (at.Date == today)
                return "hoje, " + at.ToString("HH:mm", pt);
            if (at.Date == today.AddDays(-1))
                return "ontem, " + at.ToString("HH:mm", pt);
            return at.ToString("dd MMM, HH:mm", pt);
        }
    }
}

/// <summary>
/// Ctrl+R: busca por texto nos comandos de todas as abas (history.jsonl). Enter cola o comando na aba atual,
/// sem executar.
/// </summary>
public sealed partial class HistorySearchViewModel(HistoryStore store, MainWindowViewModel owner) : ObservableObject
{
    private string? _folder;

    public ObservableCollection<HistoryItem> Results { get; } = [];

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _query = "";

    /// <summary>Só os comandos rodados na pasta da aba atual.</summary>
    [ObservableProperty]
    private bool _onlyThisFolder;

    [ObservableProperty]
    private HistoryItem? _selected;

    [ObservableProperty]
    private string _countText = "";

    public void Open(string? folder)
    {
        _folder = folder;
        Query = "";
        OnlyThisFolder = false;
        Search();
        IsOpen = true;
    }

    partial void OnQueryChanged(string value) => Search();

    partial void OnOnlyThisFolderChanged(bool value) => Search();

    private void Search()
    {
        Results.Clear();
        foreach (var entry in store.Search(Query, OnlyThisFolder ? _folder : null))
            Results.Add(new HistoryItem(entry));
        Selected = Results.FirstOrDefault();
        CountText = Results.Count == 1 ? "1 comando" : $"{Results.Count} comandos";
    }

    [RelayCommand]
    private void Paste()
    {
        if (Selected is not { } item)
            return;
        IsOpen = false;
        owner.PasteToCurrent(item.Command);
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;
        var index = Selected is null ? 0 : Results.IndexOf(Selected) + delta;
        Selected = Results[Math.Clamp(index, 0, Results.Count - 1)];
    }
}
