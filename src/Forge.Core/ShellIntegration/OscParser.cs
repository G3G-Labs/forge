using System.Globalization;
using System.Text;
using Forge.Core.Shells;

namespace Forge.Core.ShellIntegration;

/// <summary>
/// Lê a saída do terminal em pedaços (como chega do ConPTY) e reconhece as sequências OSC dos scripts de
/// integração. Uma sequência pode vir partida entre dois pedaços. Não altera nem consome a saída: o terminal
/// continua recebendo tudo e ignora o que não desenha.
/// Com <paramref name="nonce"/> (o da sessão, em <see cref="LaunchPlan.Nonce"/>), o texto do comando (633;E) e a pasta
/// (633;P;Cwd=) só valem se terminarem com ele, e o OSC 7 é ignorado: a saída de um programa (cat, curl, git log) não
/// consegue pôr comando no histórico nem apontar a aba para uma pasta de rede. Sem nonce, aceita tudo (testes).
/// </summary>
public sealed class OscParser(string? nonce = null)
{
    private const char Esc = '\x1b';
    private const char Bel = '\x07';
    private const int MaxLength = 8192;

    private enum State
    {
        Ground,
        Escape,
        Osc,
        OscEscape,
    }

    private readonly StringBuilder _osc = new();
    private State _state;
    private bool _commandRunning;

    public IReadOnlyList<ShellEvent> Feed(ReadOnlySpan<char> chunk)
    {
        List<ShellEvent>? events = null;
        foreach (var c in chunk)
        {
            switch (_state)
            {
                case State.Ground:
                    if (c == Esc)
                        _state = State.Escape;
                    break;

                case State.Escape:
                    if (c == ']')
                    {
                        _osc.Clear();
                        _state = State.Osc;
                    }
                    else
                    {
                        _state = c == Esc ? State.Escape : State.Ground;
                    }
                    break;

                case State.Osc:
                    if (c == Bel)
                    {
                        Complete(ref events);
                    }
                    else if (c == Esc)
                    {
                        _state = State.OscEscape;
                    }
                    else if (_osc.Length < MaxLength)
                    {
                        _osc.Append(c);
                    }
                    else
                    {
                        // Longa demais para ser nossa (ex.: imagem inline): descarta.
                        _state = State.Ground;
                    }
                    break;

                case State.OscEscape:
                    // ESC \ (ST) também encerra uma OSC.
                    if (c == '\\')
                        Complete(ref events);
                    else
                        _state = c == ']' ? Restart() : State.Ground;
                    break;
            }
        }
        return events ?? (IReadOnlyList<ShellEvent>)[];
    }

    private State Restart()
    {
        _osc.Clear();
        return State.Osc;
    }

    private void Complete(ref List<ShellEvent>? events)
    {
        _state = State.Ground;
        if (Interpret(_osc.ToString()) is { } e)
            (events ??= []).Add(e);
    }

    private ShellEvent? Interpret(string body)
    {
        if (body.StartsWith("133;", StringComparison.Ordinal))
        {
            var rest = body.AsSpan(4);
            switch (rest.Length > 0 ? rest[0] : '\0')
            {
                case 'A':
                    return new PromptStarted();
                case 'C':
                    _commandRunning = true;
                    return new CommandStarted();
                case 'D':
                    // O primeiro prompt também emite D, sem comando antes: não é fim de nada.
                    if (!_commandRunning)
                        return null;
                    _commandRunning = false;
                    var code = rest.Length > 2 && rest[1] == ';'
                        && int.TryParse(rest[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                            ? n
                            : (int?)null;
                    return new CommandFinished(code);
                default:
                    return null;
            }
        }
        if (body.StartsWith("633;E;", StringComparison.Ordinal))
            return Signed(body[6..]) is { } command ? new CommandLineReceived(Unescape(command)) : null;
        if (body.StartsWith("633;P;Cwd=", StringComparison.Ordinal))
            return Signed(body[10..]) is { } uri && PathMapper.FromFileUri(uri) is { } cwd ? new CwdChanged(cwd) : null;
        if (nonce is null && body.StartsWith("7;", StringComparison.Ordinal))
            return PathMapper.FromFileUri(body[2..]) is { } path ? new CwdChanged(path) : null;
        return null;
    }

    /// <summary>
    /// O valor sem o ";nonce" do fim, ou null se o nonce não confere. O valor pode ter ";" (uma pasta, por exemplo):
    /// o nonce é sempre o último campo.
    /// </summary>
    private string? Signed(string value)
    {
        if (nonce is null)
            return value;
        var separator = value.LastIndexOf(';');
        return separator >= 0 && value.AsSpan(separator + 1).SequenceEqual(nonce) ? value[..separator] : null;
    }

    /// <summary>Desfaz o escape dos scripts: "\\" vira "\" e "\xNN" vira o caractere (";" e quebras de linha).</summary>
    internal static string Unescape(ReadOnlySpan<char> text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                if (text[i + 1] == '\\')
                {
                    sb.Append('\\');
                    i++;
                    continue;
                }
                if (text[i + 1] == 'x' && i + 3 < text.Length
                    && int.TryParse(text.Slice(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    sb.Append((char)value);
                    i += 3;
                    continue;
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
