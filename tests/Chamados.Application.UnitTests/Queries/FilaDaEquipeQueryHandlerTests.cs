using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// FilaDaEquipeQueryHandler: filtro por linha (arquitetura/14) — a equipe é a do próprio ator,
/// resolvida via IEquipeDoUsuarioResolver, nunca vinda do cliente. A checagem de papel
/// Técnico/Supervisor é [Authorize(Roles=...)] na Api (leitura A, arquitetura/12), coberta em
/// Api.IntegrationTests. Ator sem equipe vinculada devolve Result.Failure.
/// </summary>
public class FilaDaEquipeQueryHandlerTests
{
    [Fact]
    public async Task Deve_consultar_pela_equipe_do_usuario_autenticado_e_repassar_a_pagina_da_leitura()
    {
        var equipeDoTecnico = Guid.NewGuid();
        var tecnicoId = Guid.NewGuid();
        var pagina = new PagedResult<ChamadoResumoDto>([ChamadoResumoFactory.Novo(equipeDoTecnico)], TotalItems: 7);

        var leitura = new FakeChamadoLeitura()
            .ComPaginaDaEquipe(equipeDoTecnico, pagina)
            .ComPaginaDaEquipe(Guid.NewGuid(), new PagedResult<ChamadoResumoDto>([ChamadoResumoFactory.Novo()], 1));
        var equipeResolver = new FakeEquipeDoUsuarioResolver().ComEquipe(tecnicoId, equipeDoTecnico);
        var currentUser = new FakeCurrentUser { UserId = tecnicoId }.ComPapel("Tecnico");
        var handler = new FilaDaEquipeQueryHandler(leitura, equipeResolver, currentUser);

        var resultado = await handler.Handle(new FilaDaEquipeQuery(Page: 2, PageSize: 10), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(pagina, resultado.Value);
        Assert.Equal(7, resultado.Value!.TotalItems);
        Assert.Equal((equipeDoTecnico, 2, 10), Assert.Single(leitura.ConsultasPorEquipe));
    }

    [Fact]
    public async Task Ator_sem_equipe_vinculada_deve_devolver_Result_Failure_sem_consultar_a_leitura()
    {
        var leitura = new FakeChamadoLeitura();
        var equipeResolver = new FakeEquipeDoUsuarioResolver(); // sem vínculo para nenhum ator
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var handler = new FilaDaEquipeQueryHandler(leitura, equipeResolver, currentUser);

        var resultado = await handler.Handle(new FilaDaEquipeQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, resultado.ErrorKind);
        Assert.Empty(leitura.ConsultasPorEquipe);
    }
}
