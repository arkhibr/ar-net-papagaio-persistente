using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharedKernel;

namespace Api.Bff;

/// <summary>
/// DbContext técnico da borda (BFF), não de um módulo de negócio (adaptacao-bff-angular.md, B6).
/// Guarda o que arquitetura/11 põe no servidor: User/SecurityVersion, AuthSession,
/// ExternalIdentity, os papéis (decisão D2 = tabela interna), a credencial do provedor local
/// provisório (B10) e as chaves de Data Protection que cifram o cookie (arquitetura/10).
/// </summary>
internal sealed class BffDbContext : DbContext, IDataProtectionKeyContext
{
    public BffDbContext(DbContextOptions<BffDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<UsuarioPapel> UsuarioPapeis => Set<UsuarioPapel>();
    public DbSet<CredencialLocal> CredenciaisLocais => Set<CredencialLocal>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// O provider SQLite não compara DateTimeOffset no SQL (o expurgo de sessões filtra por data):
    /// grava como DateTime UTC, como o ChamadosDbContext.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUtcDateTimeConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableDateTimeOffsetToUtcDateTimeConverter>();
    }

    private sealed class DateTimeOffsetToUtcDateTimeConverter()
        : ValueConverter<DateTimeOffset, DateTime>(
            v => v.UtcDateTime,
            v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

    private sealed class NullableDateTimeOffsetToUtcDateTimeConverter()
        : ValueConverter<DateTimeOffset?, DateTime?>(
            v => v.HasValue ? v.Value.UtcDateTime : null,
            v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : null);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(User.TamanhoMaximoDoNome);
        });

        modelBuilder.Entity<AuthSession>(e =>
        {
            e.ToTable("AuthSessions");
            e.HasKey(x => x.SessionId);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ExternalIdentity>(e =>
        {
            e.ToTable("ExternalIdentities");
            e.HasKey(x => new { x.Provider, x.Issuer, x.ExternalSubject });
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Provider).HasMaxLength(50);
            e.Property(x => x.Issuer).HasMaxLength(200);
            e.Property(x => x.ExternalSubject).HasMaxLength(200);
        });

        modelBuilder.Entity<UsuarioPapel>(e =>
        {
            e.ToTable("UsuarioPapeis");
            e.HasKey(x => new { x.UserId, x.Papel });
            e.Property(x => x.Papel).HasMaxLength(50);
        });

        modelBuilder.Entity<CredencialLocal>(e =>
        {
            e.ToTable("CredenciaisLocais");
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(200);
        });
    }
}

/// <summary>Usuário interno (arquitetura/11). Incrementar SecurityVersion derruba todas as sessões.</summary>
internal sealed class User
{
    public const int TamanhoMaximoDoNome = 200;

    public Guid Id { get; set; }

    /// <summary>Nome de exibição, só para UX (GET /api/me, diretório). Null até alguém definir.</summary>
    public string? Nome { get; set; }

    public int SecurityVersion { get; set; } = 1;
}

/// <summary>Sessão server-side (arquitetura/11, "Sessão server-side"). O cookie só carrega o SessionId.</summary>
internal sealed class AuthSession
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public int SecurityVersion { get; set; }
}

/// <summary>Vínculo provedor → UserId interno. O provedor local é um deles (Provider = "local").</summary>
internal sealed class ExternalIdentity
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string ExternalSubject { get; set; } = string.Empty;
}

/// <summary>Papel do usuário (decisão D2): resolvido a cada requisição, nunca gravado no cookie (B8).</summary>
internal sealed class UsuarioPapel
{
    public Guid UserId { get; set; }
    public string Papel { get; set; } = string.Empty;
}

/// <summary>Credencial do provedor local provisório (B10). Login normalizado (trim + minúsculas).</summary>
internal sealed class CredencialLocal
{
    public Guid UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public int TentativasFalhas { get; set; }
    public DateTimeOffset? BloqueadoAte { get; set; }

    public static string Normalizar(string login) => login.Trim().ToLowerInvariant();
}

/// <summary>
/// Schema do BFF por EnsureCreated, como os módulos (nenhuma migration existe ainda no repositório).
/// </summary>
internal sealed class BffDatabaseInitializer : IDatabaseInitializer
{
    private readonly BffDbContext _dbContext;

    public BffDatabaseInitializer(BffDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken) =>
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
}
