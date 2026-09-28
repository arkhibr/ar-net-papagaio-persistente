using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Bff;

/// <summary>Sessão válida e os papéis resolvidos no servidor para a requisição atual.</summary>
internal sealed record SessaoValida(AuthSession Sessao, IReadOnlyList<string> Papeis, string? Nome);

/// <summary>
/// Sessão server-side (arquitetura/11, "Sessão server-side" e SecurityVersion; M2 de achados.md;
/// adaptacao-bff-angular.md, B7). Todo provedor de login (dev, local, IdP) termina em
/// <see cref="CriarAsync"/>; o cookie carrega só sid + UserId.
/// </summary>
internal sealed class SessoesDeAutenticacao
{
    public const string ClaimSessao = "sid";

    private readonly BffDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly SessaoOptions _options;

    public SessoesDeAutenticacao(BffDbContext dbContext, TimeProvider timeProvider, IOptions<SessaoOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<AuthSession> CriarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var agora = _timeProvider.GetUtcNow();
        var sessao = new AuthSession
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = agora,
            ExpiresAt = agora + _options.Duracao,
            SecurityVersion = usuario.SecurityVersion,
        };

        _dbContext.AuthSessions.Add(sessao);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return sessao;
    }

    /// <summary>
    /// Null quando a sessão não existe, foi revogada, expirou, pertence a outro usuário ou tem
    /// SecurityVersion diferente da do User. Uma consulta por requisição (aceito pelo doc 11).
    /// </summary>
    public async Task<SessaoValida?> ValidarAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        var linha = await (
                from sessao in _dbContext.AuthSessions.AsNoTracking()
                join usuario in _dbContext.Users.AsNoTracking() on sessao.UserId equals usuario.Id
                where sessao.SessionId == sessionId
                select new { Sessao = sessao, usuario.SecurityVersion, usuario.Nome })
            .SingleOrDefaultAsync(cancellationToken);

        if (linha is null
            || linha.Sessao.UserId != userId
            || linha.Sessao.RevokedAt is not null
            || linha.Sessao.ExpiresAt <= _timeProvider.GetUtcNow()
            || linha.Sessao.SecurityVersion != linha.SecurityVersion)
        {
            return null;
        }

        var papeis = await _dbContext.UsuarioPapeis.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Papel)
            .ToListAsync(cancellationToken);

        return new SessaoValida(linha.Sessao, papeis, linha.Nome);
    }

    public async Task RevogarAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var sessao = await _dbContext.AuthSessions.SingleOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (sessao is { RevokedAt: null })
        {
            sessao.RevokedAt = _timeProvider.GetUtcNow();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Apaga sessões encerradas (expiradas ou revogadas) há mais de Sessao:RetencaoAposEncerramento.
    /// Seguro com várias réplicas: é um DELETE condicional, idempotente.
    /// </summary>
    public async Task<int> ExpurgarAsync(CancellationToken cancellationToken)
    {
        var limite = _timeProvider.GetUtcNow() - _options.RetencaoAposEncerramento;
        return await _dbContext.AuthSessions
            .Where(s => s.ExpiresAt < limite || (s.RevokedAt != null && s.RevokedAt < limite))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Principal gravado no cookie: só identidade e sessão, nunca papel (B8).</summary>
    public static ClaimsPrincipal CriarPrincipal(AuthSession sessao) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, sessao.UserId.ToString()),
                new Claim(ClaimSessao, sessao.SessionId.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));
}
