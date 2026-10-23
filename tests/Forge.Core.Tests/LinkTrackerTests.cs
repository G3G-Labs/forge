using Forge.Core.ShellIntegration;

namespace Forge.Core.Tests;

public class LinkTrackerTests
{
    [Fact]
    public void Acha_o_endereco_do_servidor_mesmo_colorido()
    {
        var links = new LinkTracker();

        // Saída do Vite: o link vem pintado e em negrito, com a porta separada por cores.
        links.Feed("  \x1b[32m➜\x1b[39m  \x1b[1mLocal\x1b[22m:   \x1b[36mhttp://localhost:\x1b[1m5173\x1b[22m/\x1b[39m\r\n");

        Assert.Equal(["http://localhost:5173/"], links.Links);
    }

    [Fact]
    public void Link_partido_entre_dois_pedacos_e_juntado()
    {
        var links = new LinkTracker();

        links.Feed("abra https://github.com/G3G-La");
        Assert.Empty(links.Links);
        links.Feed("bs/forge/pull/1 para revisar\r\n");

        Assert.Equal(["https://github.com/G3G-Labs/forge/pull/1"], links.Links);
    }

    [Fact]
    public void Mais_recente_primeiro_sem_repetir_e_com_limite()
    {
        var links = new LinkTracker(capacity: 3);

        links.Feed("http://a.com\nhttp://b.com\nhttp://a.com\nhttp://c.com\nhttp://d.com\n");

        Assert.Equal(["http://d.com", "http://c.com", "http://a.com"], links.Links);
    }

    [Theory]
    [InlineData("veja https://x.com/a.", "https://x.com/a")]
    [InlineData("(https://x.com/a)", "https://x.com/a")]
    [InlineData("https://pt.wikipedia.org/wiki/Forja_(metalurgia)", "https://pt.wikipedia.org/wiki/Forja_(metalurgia)")]
    [InlineData("\"https://x.com/?q=1&b=2\"", "https://x.com/?q=1&b=2")]
    public void Pontuacao_da_frase_fica_fora_do_link(string text, string expected) =>
        Assert.Equal([expected], LinkTracker.Find(text));

    [Fact]
    public void Texto_sem_link_e_esquemas_que_nao_sao_web_ficam_de_fora()
    {
        Assert.Empty(LinkTracker.Find("file:///C:/Windows ftp://x.com javascript:alert(1) https://"));
    }

    [Theory]
    [InlineData("https://localhost:5173/", true)]
    [InlineData("http://x.com", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("localhost:5173", false)]
    public void So_http_e_https_abrem_no_navegador(string url, bool expected) =>
        Assert.Equal(expected, Forge.Core.Actions.ExternalApps.IsWebLink(url));
}
