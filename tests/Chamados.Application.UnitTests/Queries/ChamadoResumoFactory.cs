using Chamados.Contracts;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>Monta ChamadoResumoDto para os testes das listagens.</summary>
internal static class ChamadoResumoFactory
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    public static ChamadoResumoDto Novo(Guid? equipeId = null) =>
        new(
            Id: Guid.NewGuid(),
            CategoriaId: Guid.NewGuid(),
            EquipeId: equipeId ?? Guid.NewGuid(),
            Prioridade: PrioridadeChamado.Media,
            Status: StatusChamado.Aberto,
            AbertoEm: AbertoEm,
            PrazoSla: AbertoEm.AddHours(24),
            TecnicoAtribuidoId: null,
            Escalonado: false);
}
