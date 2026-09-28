using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure;

/// <summary>Mapeamento EF Core de Equipe. Mesmo acesso por campo de CategoriaDeServicoConfiguration.</summary>
internal sealed class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("Equipes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.Nome)
            .IsRequired()
            .HasMaxLength(Equipe.TamanhoMaximoDoNome);

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
