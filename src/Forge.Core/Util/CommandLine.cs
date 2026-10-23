using System.Text;

namespace Forge.Core.Util;

/// <summary>Monta linhas de comando do Windows com as regras de aspas do CommandLineToArgvW.</summary>
public static class CommandLine
{
    public static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0)
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
}
