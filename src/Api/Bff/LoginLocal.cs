using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Bff;

/// <summary>
/// Provedor local de credenciais provisório (decisão D1; arquitetura/11, Nota de aplicação;
/// adaptacao-bff-angular.md, B10). Devolve só o UserId: sessão e cookie são os mesmos de qualquer
/// outro provedor. Login inexistente, senha errada e conta bloqueada são indistinguíveis para
/// quem chama.
/// </summary>
internal sealed class LoginLocal
{
    // Hash fixo verificado quando o login não existe, para o tempo de resposta não revelar isso.
    private static readonly Lazy<string> HashFicticio = new(() =>
        new PasswordHasher<CredencialLocal>().HashPassword(new CredencialLocal(), Guid.NewGuid().ToString()));

    private readonly BffDbContext _dbContext;
    private readonly IPasswordHasher<CredencialLocal> _hasher;
    private readonly TimeProvider _timeProvider;
    private readonly LoginLocalOptions _options;
    private readonly ILogger<LoginLocal> _logger;

    public LoginLocal(
        BffDbContext dbContext,
        IPasswordHasher<CredencialLocal> hasher,
        TimeProvider timeProvider,
        IOptions<LoginLocalOptions> options,
        ILogger<LoginLocal> logger)
    {
        _dbContext = dbContext;
        _hasher = hasher;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid?> AutenticarAsync(string? login, string? senha, CancellationToken cancellationToken)
    {
        var normalizado = CredencialLocal.Normalizar(login ?? string.Empty);
        senha ??= string.Empty;
        var credencial = normalizado.Length == 0
            ? null
            : await _dbContext.CredenciaisLocais.SingleOrDefaultAsync(c => c.Login == normalizado, cancellationToken);

        if (credencial is null)
        {
            _hasher.VerifyHashedPassword(new CredencialLocal(), HashFicticio.Value, senha);
            return null;
        }

        var agora = _timeProvider.GetUtcNow();
        var resultado = _hasher.VerifyHashedPassword(credencial, credencial.SenhaHash, senha);

        if (credencial.BloqueadoAte > agora)
        {
            return null;
        }

        if (resultado == PasswordVerificationResult.Failed)
        {
            credencial.TentativasFalhas++;
            if (credencial.TentativasFalhas >= _options.MaximoDeTentativas)
            {
                credencial.BloqueadoAte = agora + _options.DuracaoDoBloqueio;
                credencial.TentativasFalhas = 0;
                _logger.LogInformation("Login local bloqueado até {BloqueadoAte} para {UserId}", credencial.BloqueadoAte, credencial.UserId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credencial.SenhaHash = _hasher.HashPassword(credencial, senha);
        }

        credencial.TentativasFalhas = 0;
        credencial.BloqueadoAte = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Login local bem-sucedido para {UserId}", credencial.UserId);
        return credencial.UserId;
    }
}
