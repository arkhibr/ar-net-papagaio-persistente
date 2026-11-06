using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// AbrirChamadoCommand.IsAuthorizedAsync: o SolicitanteId precisa ser o próprio usuário
/// autenticado, para qualquer chamador de Contracts (item 7 das melhorias; mesma regra de
/// AtribuirChamadoCommand, A1 de achados.md).
/// </summary>
public class AbrirChamadoCommandAuthorizationTests
{
    private static AbrirChamadoCommand NovoCommand(Guid solicitanteId) =>
        new(solicitanteId, Guid.NewGuid(), PrioridadeChamado.Media, "chave-abrir");

    private static Task<bool> AutorizadoAsync(AbrirChamadoCommand command, FakeCurrentUser currentUser) =>
        command.IsAuthorizedAsync(currentUser, new FakeAuthorizationContext(), CancellationToken.None);

    [Fact]
    public async Task Usuario_abrindo_em_nome_proprio_deve_ser_autorizado()
    {
        var currentUser = new FakeCurrentUser();

        Assert.True(await AutorizadoAsync(NovoCommand(currentUser.UserId), currentUser));
    }

    [Fact]
    public async Task Abrir_em_nome_de_outra_pessoa_nao_deve_ser_autorizado()
    {
        Assert.False(await AutorizadoAsync(NovoCommand(Guid.NewGuid()), new FakeCurrentUser()));
    }

    [Fact]
    public async Task Usuario_nao_autenticado_nao_deve_ser_autorizado()
    {
        var currentUser = new FakeCurrentUser { IsAuthenticated = false };

        Assert.False(await AutorizadoAsync(NovoCommand(currentUser.UserId), currentUser));
    }

    [Fact]
    public async Task Ator_de_sistema_nao_abre_chamado()
    {
        var sistema = FakeCurrentUser.SistemaAutomatizado();

        Assert.False(await AutorizadoAsync(NovoCommand(sistema.UserId), sistema));
    }

    [Fact]
    public async Task SolicitanteId_vazio_nao_deve_ser_autorizado()
    {
        var currentUser = new FakeCurrentUser { UserId = Guid.Empty };

        Assert.False(await AutorizadoAsync(NovoCommand(Guid.Empty), currentUser));
    }
}
