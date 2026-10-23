using Forge.Core.Appearance;
using Forge.Core.Git;

namespace Forge.Core.Tests;

public class GitAndThemeTests
{
    [Fact]
    public void Le_branch_alteracoes_e_commits_a_frente_e_atras()
    {
        const string output = """
            # branch.oid 1a2b3c4d5e6f7a8b9c0d
            # branch.head feat/pedidos
            # branch.upstream origin/feat/pedidos
            # branch.ab +2 -1
            1 .M N... 100644 100644 100644 aaa bbb src/server.ts
            1 M. N... 100644 100644 100644 aaa bbb src/rotas/pedidos.ts
            ? src/rotas/estoque.ts
            ! dist/
            """;

        var status = GitStatusParser.Parse(output);

        Assert.Equal("feat/pedidos", status.Branch);
        Assert.Equal("origin/feat/pedidos", status.Upstream);
        Assert.Equal(2, status.Ahead);
        Assert.Equal(1, status.Behind);
        Assert.Equal(3, status.Changes);
        Assert.False(status.Detached);
    }

    [Fact]
    public void HEAD_solto_mostra_o_commit_curto()
    {
        var status = GitStatusParser.Parse("# branch.oid 1a2b3c4d5e6f\n# branch.head (detached)\n");

        Assert.True(status.Detached);
        Assert.Equal("1a2b3c4", status.Branch);
    }

    [Fact]
    public void Importa_esquemas_do_settings_json_do_Windows_Terminal_com_comentarios()
    {
        const string json = """
            {
              // comentário, como no settings.json real
              "profiles": {},
              "schemes": [
                {
                  "name": "One Half Dark",
                  "background": "#282C34", "foreground": "#DCDFE4", "cursorColor": "#FFFFFF", "selectionBackground": "#FFFFFF",
                  "black": "#282C34", "red": "#E06C75", "green": "#98C379", "yellow": "#E5C07B",
                  "blue": "#61AFEF", "purple": "#C678DD", "cyan": "#56B6C2", "white": "#DCDFE4",
                  "brightBlack": "#5A6374", "brightRed": "#E06C75", "brightGreen": "#98C379", "brightYellow": "#E5C07B",
                  "brightBlue": "#61AFEF", "brightPurple": "#C678DD", "brightCyan": "#56B6C2", "brightWhite": "#DCDFE4",
                },
                { "name": "Incompleto", "background": "#000000" }
              ]
            }
            """;

        var scheme = Assert.Single(WindowsTerminalImport.Parse(json));

        Assert.Equal("One Half Dark", scheme.Name);
        Assert.Equal(16, scheme.Palette.Count);
        Assert.Equal("#E06C75", scheme.Palette[1]);
    }
}
