using Forge.Core.ShellIntegration;
using Forge.Core.Workspaces;

namespace Forge.Core.Tests;

public class TabStatusTests
{
    [Fact]
    public void Comando_que_falha_deixa_a_aba_em_erro_e_vai_para_o_historico()
    {
        var status = new TabStatus();
        status.ShellStarted();
        status.Apply(new PromptStarted());
        status.Apply(new CwdChanged("/home/dev/api"));
        status.Apply(new CommandLineReceived("npm start"));
        status.Apply(new CommandStarted());
        Assert.Equal(TabRunState.Running, status.State);

        var finished = status.Apply(new CommandFinished(1));

        Assert.Equal(TabRunState.Failed, status.State);
        Assert.Equal(new FinishedCommand("npm start", 1, "/home/dev/api"), finished);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(130)]
    [InlineData(unchecked((int)0xC000013A))]
    public void Sucesso_ou_Ctrl_C_volta_para_parada(int code)
    {
        var status = new TabStatus();
        status.ShellStarted();
        status.Apply(new CommandStarted());

        status.Apply(new CommandFinished(code));

        Assert.Equal(TabRunState.Idle, status.State);
    }

    [Fact]
    public void Fica_pronta_no_primeiro_prompt()
    {
        var status = new TabStatus();
        status.ShellStarted();
        Assert.False(status.IsReady);

        status.Apply(new PromptStarted());

        Assert.True(status.IsReady);
    }

    [Fact]
    public void Sem_integracao_o_Ctrl_C_do_app_marca_como_parada()
    {
        var status = new TabStatus();
        status.ShellStarted();
        status.CommandSent();

        status.AssumeStopped();

        Assert.Equal(TabRunState.Idle, status.State);
    }
}
