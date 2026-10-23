using System.Text.RegularExpressions;

namespace Forge.Core.ShellIntegration;

/// <summary>
/// Os links http(s) que apareceram na saída do terminal, do mais recente para o mais antigo, sem repetir: o
/// "http://localhost:5173" do bun dev, o endereço de um PR no git push… O menu do clique direito oferece abrir no
/// navegador. O controle nativo do terminal não diz qual texto está sob o mouse, então o Ctrl+clique não é possível.
/// Lê a saída em pedaços: um link (ou uma sequência de cor) partido entre dois pedaços é juntado antes de procurar.
/// </summary>
public sealed partial class LinkTracker(int capacity = 10)
{
    // O que sobrou do pedaço anterior depois da última quebra de linha (um link pode continuar no próximo).
    private const int MaxCarry = 4096;

    private readonly List<string> _links = [];
    private string _carry = "";

    /// <summary>Mais recente primeiro.</summary>
    public IReadOnlyList<string> Links => _links;

    public void Feed(ReadOnlySpan<char> chunk)
    {
        var text = _carry + chunk.ToString();
        // A última linha pode estar pela metade: fica para o próximo pedaço, a não ser que seja grande demais.
        var lastBreak = text.LastIndexOfAny(['\n', '\r']);
        var complete = lastBreak >= 0 ? text[..(lastBreak + 1)] : "";
        _carry = lastBreak >= 0 ? text[(lastBreak + 1)..] : text;
        if (_carry.Length > MaxCarry)
        {
            complete = text;
            _carry = "";
        }
        if (complete.Length > 0)
            Scan(complete);
    }

    /// <summary>Os links de um texto (a seleção do usuário, por exemplo), na ordem em que aparecem.</summary>
    public static IReadOnlyList<string> Find(string text) => Matches(text).Distinct().ToList();

    // Todas as ocorrências, na ordem: um link que se repete mais adiante na saída é o mais recente.
    private static IEnumerable<string> Matches(string text) =>
        Url().Matches(Escapes().Replace(text, "")).Select(m => Trim(m.Value)).Where(u => u.Length > "https://".Length);

    private void Scan(string text)
    {
        foreach (var url in Matches(text))
        {
            _links.Remove(url);
            _links.Insert(0, url);
        }
        if (_links.Count > capacity)
            _links.RemoveRange(capacity, _links.Count - capacity);
    }

    // Pontuação no fim é da frase, não do link: "veja https://x.com/a." e "(https://x.com/a)".
    private static string Trim(string url)
    {
        url = url.TrimEnd('.', ',', ';', ':', '!', '?', '\'', '"');
        while (url.EndsWith(')') && url.Count(c => c == '(') < url.Count(c => c == ')'))
            url = url[..^1];
        return url;
    }

    // Cores e movimentos de cursor (CSI), títulos e integração (OSC) e escapes simples.
    [GeneratedRegex(@"\x1b\[[0-9;?<=>!]*[ -/]*[@-~]|\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)|\x1b[@-Z\\-_]")]
    private static partial Regex Escapes();

    [GeneratedRegex(@"\bhttps?://[^\s""'<>`\x00-\x1f\x7f]+", RegexOptions.IgnoreCase)]
    private static partial Regex Url();
}
