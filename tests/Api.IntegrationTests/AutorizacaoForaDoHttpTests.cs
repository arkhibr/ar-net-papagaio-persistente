using Catalogo.Contracts;
using Chamados.Contracts;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.IntegrationTests;

/// <summary>
/// Itens 7 e 8 das melhorias: um chamador de Contracts que não passa pelo HTTP (outro módulo, um
/// job) também é barrado pelo AuthorizationBehavior, com o container real. Sem HttpContext, o
/// ICurrentUser é anônimo.
/// </summary>
public sealed class AutorizacaoForaDoHttpTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<TResposta> EnviarAsync<TResposta>(IRequest<TResposta> mensagem)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(mensagem);
    }

    [Fact]
    public async Task Abrir_chamado_em_nome_de_outra_pessoa_e_negado_antes_de_reservar_a_chave()
    {
        var command = new AbrirChamadoCommand(Guid.NewGuid(), _cenario.CategoriaId, PrioridadeChamado.Media, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<AuthorizationDeniedException>(() => EnviarAsync(command));
        Assert.Equal(0, _factory.ContarLinhas(_factory.ChamadosDb, "Chamados"));
        Assert.Equal(0, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosIdempotencyRecords"));
    }

    [Fact]
    public async Task Administracao_do_catalogo_fora_do_http_e_negada()
    {
        await Assert.ThrowsAsync<AuthorizationDeniedException>(() => EnviarAsync(new CriarEquipeCommand("Infra")));
        await Assert.ThrowsAsync<AuthorizationDeniedException>(() => EnviarAsync(new EquipesQuery()));
        Assert.Equal(0, _factory.ContarLinhas(_factory.CatalogoDb, "Equipes"));
    }
}
