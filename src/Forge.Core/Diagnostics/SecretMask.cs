using System.Text.RegularExpressions;

namespace Forge.Core.Diagnostics;

/// <summary>
/// Esconde senhas e tokens de um comando antes de ele ir para o log de auditoria, que fica 14 dias em texto puro.
/// É uma rede de proteção por padrões conhecidos, não uma garantia: o histórico do Ctrl+R guarda o comando como o shell
/// guardou, e o que o shell deixa fora do histórico (linha começando com espaço…) nem chega aqui.
/// </summary>
public static partial class SecretMask
{
    private const string Hidden = "***";

    public static string Apply(string text)
    {
        text = AuthorizationHeader().Replace(text, "$1" + Hidden);
        text = UrlCredentials().Replace(text, "$1" + Hidden + "@");
        text = SecretAssignment().Replace(text, "$1" + Hidden);
        text = SecretFlag().Replace(text, "$1" + Hidden);
        text = MySqlPassword().Replace(text, "$1" + Hidden);
        return text;
    }

    // Authorization: Bearer abc / Basic abc / Token abc
    [GeneratedRegex(@"(?i)(\bauthorization:\s*(?:bearer|basic|token)\s+)[^\s""']+")]
    private static partial Regex AuthorizationHeader();

    // https://usuario:senha@host
    [GeneratedRegex(@"(?i)(\b[a-z][a-z0-9+.-]*://[^/\s:@]+:)[^@\s/]+@")]
    private static partial Regex UrlCredentials();

    // PASSWORD=x, $env:GITHUB_TOKEN = 'x', --api-key=x, "X-Api-Key: x"
    [GeneratedRegex(@"(?i)([\w.-]*(?:password|passwd|pwd|secret|token|api[-_]?key|access[-_]?key|private[-_]?key|credentials?)[\w.-]*\s*[=:]\s*)(?:""[^""]*""|'[^']*'|[^\s""']+)")]
    private static partial Regex SecretAssignment();

    // --password x, -Token x (o valor separado por espaço)
    [GeneratedRegex(@"(?i)(\s--?[\w-]*(?:password|passwd|secret|token|api[-_]?key)[\w-]*\s+)(?:""[^""]*""|'[^']*'|[^\s""'-][^\s""']*)")]
    private static partial Regex SecretFlag();

    // mysql -pSenha (no mysql e no mariadb, -p colado é a senha)
    [GeneratedRegex(@"(?i)(\b(?:mysql|mysqldump|mysqladmin|mariadb)\b[^\n]*?\s-p)(?=[^\s-])\S+")]
    private static partial Regex MySqlPassword();
}
