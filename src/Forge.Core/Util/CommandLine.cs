using System.Text;

namespace Forge.Core.Util;

/// <summary>Monta linhas de comando do Windows com as regras de aspas do CommandLineToArgvW.</summary>
public static class CommandLine
{
    // Além de espaço e aspas, os caracteres especiais do cmd.exe também ganham aspas: entre aspas, & | < > ^ ( ) não
    // separam comandos. Para o CommandLineToArgvW, aspas a mais não mudam nada.
    private static readonly char[] NeedsQuotes = [' ', '\t', '"', '&', '|', '<', '>', '^', '(', ')', '%', '!'];

    public static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny(NeedsQuotes) < 0)
            return arg;

        var sb = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            // Barras antes de aspas são dobradas e a aspa é escapada; antes de outro caractere, ficam como estão.
            sb.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
            sb.Append(c);
            backslashes = 0;
        }
        // Barras no fim antecedem a aspa de fechamento: também dobram.
        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }

    public static string Join(string fileName, IEnumerable<string> args) =>
        string.Join(' ', args.Select(Quote).Prepend(Quote(fileName)));

    /// <summary>
    /// O argumento pode ir para uma linha do <c>cmd.exe /c</c>, já com <see cref="Quote"/>. Aspas não: o cmd não entende
    /// o \" do Windows e o resto da linha sairia das aspas. Nem %: o cmd expande %VARIAVEL% mesmo entre aspas.
    /// Nem caracteres de controle (quebra de linha encerra o comando).
    /// </summary>
    public static bool IsSafeForCmd(string arg) => !arg.Any(c => c is '"' or '%' || char.IsControl(c));
}
