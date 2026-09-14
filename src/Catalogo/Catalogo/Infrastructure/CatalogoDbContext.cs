using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

/// <summary>
/// DbContext do módulo Catalogo. Só mapeia entidades do próprio módulo (CategoriaDeServico,
/// MembroDeEquipe) — nunca um DbSet/IEntityTypeConfiguration de outro módulo (Chamados), regra
/// sem exceção de arquitetura/04-comunicacao-entre-modulos.md e arquitetura/01. internal:
/// Catalogo permanece 2 projetos, Infrastructure fundida no mesmo assembly (arquitetura/01).
/// </summary>
internal sealed class CatalogoDbContext : DbContext
{
    public CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : base(options)
    {
    }

    public DbSet<CategoriaDeServico> CategoriasDeServico => Set<CategoriaDeServico>();

    public DbSet<MembroDeEquipe> MembrosDeEquipe => Set<MembroDeEquipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);
    }
}
