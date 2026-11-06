using Microsoft.EntityFrameworkCore;

namespace Api.Bff;

/// <summary>Usuário como a administração o vê: nome de exibição, login local (se houver) e papéis.</summary>
public sealed record UsuarioDoDiretorioDto(Guid Id, string? Nome, string? Login, IReadOnlyList<string> Papeis);

/// <summary>Só o nome de exibição, para as telas trocarem GUID por nome.</summary>
public sealed record NomeDeUsuarioDto(Guid Id, string? Nome);

/// <summary>
/// Leitura dos usuários do BFF para UX: nomes de exibição em listagens e a lista completa para a
/// administração do Catálogo escolher membros de equipe. Nunca é fonte de autorização.
/// </summary>
internal sealed class DiretorioDeUsuarios
{
    public const int MaximoDeIdsPorConsulta = 100;

    private readonly BffDbContext _dbContext;

    public DiretorioDeUsuarios(BffDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UsuarioDoDiretorioDto>> ListarAsync(CancellationToken cancellationToken)
    {
        var usuarios = await _dbContext.Users.AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.Nome,
                Login = _dbContext.CredenciaisLocais.Where(c => c.UserId == u.Id).Select(c => c.Login).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var papeis = (await _dbContext.UsuarioPapeis.AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(p => p.UserId, p => p.Papel);

        return usuarios
            .OrderBy(u => u.Nome ?? u.Login ?? u.Id.ToString(), StringComparer.CurrentCultureIgnoreCase)
            .Select(u => new UsuarioDoDiretorioDto(u.Id, u.Nome, u.Login, papeis[u.Id].Order().ToList()))
            .ToList();
    }

    /// <summary>Ids desconhecidos voltam com Nome null, para o cliente não perguntar de novo.</summary>
    public async Task<IReadOnlyList<NomeDeUsuarioDto>> NomesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var nomes = await _dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, cancellationToken);

        return ids.Distinct().Select(id => new NomeDeUsuarioDto(id, nomes.GetValueOrDefault(id))).ToList();
    }
}
