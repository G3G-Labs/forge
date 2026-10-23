using Forge.Core.Diagnostics;
using Forge.Core.ShellIntegration;

namespace Forge.Core.Tests;

public class PasteAndSecretTests
{
    [Theory]
    [InlineData("npm run dev", "npm run dev")]
    [InlineData("rm -rf ~\n", "rm -rf ~")]
    [InlineData("echo a\r\necho b", "echo a echo b")]
    [InlineData("docker run \\\n  -p 80:80 nginx", "docker run    -p 80:80 nginx")]
    [InlineData("Get-ChildItem `\n  -Recurse", "Get-ChildItem    -Recurse")]
    [InlineData("echo \x1b[31mvermelho\x03", "echo  [31mvermelho")]
    public void Colar_do_historico_vai_em_uma_linha_sem_controles(string command, string expected) =>
        Assert.Equal(expected, PasteText.SingleLine(command));

    [Theory]
    [InlineData("curl -H \"Authorization: Bearer abc.def\" https://api", "curl -H \"Authorization: Bearer ***\" https://api")]
    [InlineData("git clone https://gabriel:ghp_123@github.com/x/y", "git clone https://gabriel:***@github.com/x/y")]
    [InlineData("export GITHUB_TOKEN=ghp_123", "export GITHUB_TOKEN=***")]
    [InlineData("$env:API_KEY = 'segredo'", "$env:API_KEY = ***")]
    [InlineData("psql --password=\"a b\" -U dev", "psql --password=*** -U dev")]
    [InlineData("vault login -token s.123", "vault login -token ***")]
    [InlineData("mysql -u root -pS3nha db", "mysql -u root -p*** db")]
    [InlineData("curl -H \"X-Api-Key: abc\" https://api", "curl -H \"X-Api-Key: ***\" https://api")]
    public void Log_de_auditoria_esconde_senhas_e_tokens(string command, string expected) =>
        Assert.Equal(expected, SecretMask.Apply(command));

    [Theory]
    [InlineData("mkdir -p src/app")]
    [InlineData("npm run dev -- --port 3000")]
    [InlineData("pwd")]
    [InlineData("git log --oneline")]
    public void Comandos_comuns_ficam_como_estao(string command) =>
        Assert.Equal(command, SecretMask.Apply(command));
}
