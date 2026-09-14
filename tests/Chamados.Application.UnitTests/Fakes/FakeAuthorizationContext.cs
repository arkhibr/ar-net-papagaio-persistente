using SharedKernel;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>Fake de IAuthorizationContext: vínculos ator-recurso configurados explicitamente por teste.</summary>
public sealed class FakeAuthorizationContext : IAuthorizationContext
{
    private readonly HashSet<(Guid UserId, Guid ResourceId)> _vinculos = new();

    public FakeAuthorizationContext ComVinculo(Guid userId, Guid resourceId)
    {
        _vinculos.Add((userId, resourceId));
        return this;
    }

    public Task<bool> HasResourceLinkAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken) =>
        Task.FromResult(_vinculos.Contains((userId, resourceId)));
}
