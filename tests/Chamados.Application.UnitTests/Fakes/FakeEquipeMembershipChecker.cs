using Chamados.Application;

namespace Chamados.Application.UnitTests.Fakes;

public sealed class FakeEquipeMembershipChecker : IEquipeMembershipChecker
{
    private readonly HashSet<(Guid TecnicoId, Guid EquipeId)> _membros = new();

    public FakeEquipeMembershipChecker ComMembro(Guid tecnicoId, Guid equipeId)
    {
        _membros.Add((tecnicoId, equipeId));
        return this;
    }

    public Task<bool> EhMembroDaEquipeAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(_membros.Contains((tecnicoId, equipeId)));
}
