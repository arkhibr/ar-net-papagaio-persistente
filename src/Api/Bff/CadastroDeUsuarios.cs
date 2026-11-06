using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Bff;

/// <summary>
/// Cadastro de usuários do provedor local e do login de desenvolvimento (adaptacao-bff-angular.md,
/// B7/B8/B10). Usado pelos comandos criar-usuario/redefinir-senha, por POST /dev/login e pelos
/// testes. Não há autocadastro público.
/// </summary>
internal sealed class CadastroDeUsuarios
{
    public const string ProvedorLocal = "local";
    public const string EmissorLocal = "chamados";

    /// <summary>Papéis conhecidos pela autorização (ICurrentUser.IsInRole, [Authorize(Roles)]).</summary>
    public static readonly IReadOnlyList<string> PapeisConhecidos = ["Solicitante", "Tecnico", "Supervisor"];

    private readonly BffDbContext _dbContext;
    private readonly IPasswordHasher<CredencialLocal> _hasher;

    public CadastroDeUsuarios(BffDbContext dbContext, IPasswordHasher<CredencialLocal> hasher)
    {
        _dbContext = dbContext;
        _hasher = hasher;
    }

    /// <summary>Cria User, ExternalIdentity local, CredencialLocal e papéis. Devolve a senha gerada.</summary>
    public async Task<(Guid UserId, string Senha)> CriarUsuarioLocalAsync(
        string login, IReadOnlyCollection<string> papeis, string? nome, CancellationToken cancellationToken)
    {
        var normalizado = CredencialLocal.Normalizar(login);
        if (normalizado.Length == 0)
        {
            throw new ArgumentException("Login vazio.", nameof(login));
        }

        ValidarPapeis(papeis);
        if (await _dbContext.CredenciaisLocais.AnyAsync(c => c.Login == normalizado, cancellationToken))
        {
            throw new InvalidOperationException($"O login '{normalizado}' já existe.");
        }

        var usuario = new User { Id = Guid.NewGuid(), Nome = NomeValido(nome) };
        var senha = GerarSenha();
        var credencial = new CredencialLocal { UserId = usuario.Id, Login = normalizado };
        credencial.SenhaHash = _hasher.HashPassword(credencial, senha);

        _dbContext.Users.Add(usuario);
        _dbContext.ExternalIdentities.Add(new ExternalIdentity
        {
            UserId = usuario.Id,
            Provider = ProvedorLocal,
            Issuer = EmissorLocal,
            ExternalSubject = normalizado,
        });
        _dbContext.CredenciaisLocais.Add(credencial);
        _dbContext.UsuarioPapeis.AddRange(papeis.Distinct().Select(p => new UsuarioPapel { UserId = usuario.Id, Papel = p }));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (usuario.Id, senha);
    }

    /// <summary>Gera senha nova e incrementa SecurityVersion, derrubando as sessões abertas (B7).</summary>
    public async Task<string> RedefinirSenhaAsync(string login, CancellationToken cancellationToken)
    {
        var normalizado = CredencialLocal.Normalizar(login);
        var credencial = await _dbContext.CredenciaisLocais.SingleOrDefaultAsync(c => c.Login == normalizado, cancellationToken)
                         ?? throw new InvalidOperationException($"O login '{normalizado}' não existe.");
        var usuario = await _dbContext.Users.SingleAsync(u => u.Id == credencial.UserId, cancellationToken);

        var senha = GerarSenha();
        credencial.SenhaHash = _hasher.HashPassword(credencial, senha);
        credencial.TentativasFalhas = 0;
        credencial.BloqueadoAte = null;
        usuario.SecurityVersion++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return senha;
    }

    /// <summary>
    /// Só POST /dev/login (Development): garante o User com o id escolhido e substitui os papéis
    /// na fonte do servidor, nunca no cookie (B8).
    /// </summary>
    public async Task GarantirUsuarioDeDesenvolvimentoAsync(
        Guid userId, IReadOnlyCollection<string> papeis, string? nome, CancellationToken cancellationToken)
    {
        ValidarPapeis(papeis);
        var usuario = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (usuario is null)
        {
            usuario = new User { Id = userId };
            _dbContext.Users.Add(usuario);
        }

        if (!string.IsNullOrWhiteSpace(nome))
        {
            usuario.Nome = NomeValido(nome);
        }

        await _dbContext.UsuarioPapeis.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        _dbContext.UsuarioPapeis.AddRange(papeis.Distinct().Select(p => new UsuarioPapel { UserId = userId, Papel = p }));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Define ou troca o nome de exibição (comando definir-nome).</summary>
    public async Task DefinirNomeAsync(string login, string nome, CancellationToken cancellationToken)
    {
        var normalizado = CredencialLocal.Normalizar(login);
        var credencial = await _dbContext.CredenciaisLocais.SingleOrDefaultAsync(c => c.Login == normalizado, cancellationToken)
                         ?? throw new InvalidOperationException($"O login '{normalizado}' não existe.");
        var usuario = await _dbContext.Users.SingleAsync(u => u.Id == credencial.UserId, cancellationToken);
        usuario.Nome = NomeValido(nome) ?? throw new ArgumentException("Nome vazio.", nameof(nome));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? NomeValido(string? nome)
    {
        var limpo = nome?.Trim();
        if (string.IsNullOrEmpty(limpo))
        {
            return null;
        }

        return limpo.Length <= User.TamanhoMaximoDoNome
            ? limpo
            : throw new ArgumentException($"O nome tem até {User.TamanhoMaximoDoNome} caracteres.", nameof(nome));
    }

    private static void ValidarPapeis(IEnumerable<string> papeis)
    {
        var desconhecidos = papeis.Except(PapeisConhecidos, StringComparer.Ordinal).ToArray();
        if (desconhecidos.Length > 0)
        {
            throw new ArgumentException(
                $"Papel desconhecido: {string.Join(", ", desconhecidos)}. Use {string.Join(", ", PapeisConhecidos)}.");
        }
    }

    private static string GerarSenha()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return RandomNumberGenerator.GetString(alfabeto, 20);
    }
}
