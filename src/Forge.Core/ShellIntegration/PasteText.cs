using System.Text;

namespace Forge.Core.ShellIntegration;

/// <summary>
/// Um comando do histórico (Ctrl+R) pronto para colar sem executar. No terminal, quebra de linha é Enter: um comando
/// de várias linhas rodaria na hora, sem o usuário conferir. E ESC e outros caracteres de controle virariam teclas.
/// </summary>
public static class PasteText
{
    /// <summary>
    /// Uma linha só: as quebras viram espaço (a continuação de linha, "\" no bash e "`" no PowerShell, sai junto) e os
    /// caracteres de controle viram espaço.
    /// </summary>
    public static string SingleLine(string command)
    {
        var lines = command.ReplaceLineEndings("\n").Split('\n');
        var sb = new StringBuilder(command.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var last = i == lines.Length - 1;
            if (!last && line.Length > 0 && line[^1] is '\\' or '`')
                line = line[..^1];
            sb.Append(line);
            if (!last)
                sb.Append(' ');
        }
        for (var i = 0; i < sb.Length; i++)
            if (char.IsControl(sb[i]))
                sb[i] = ' ';
        return sb.ToString().TrimEnd();
    }
}
