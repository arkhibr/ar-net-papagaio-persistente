using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure;

/// <summary>
/// Mapeamento EF Core do vínculo técnico-equipe. Tabela nova, dado plano de referência
/// (ver MembroDeEquipe.cs e Application/IMembroDeEquipeRepository.cs). TecnicoId é chave
/// primária (um técnico pertence a no máximo uma equipe, decisão registrada em MembroDeEquipe.cs).
/// </summary>
internal sealed class MembroDeEquipeConfiguration : IEntityTypeConfiguration<MembroDeEquipe>
{
    public void Configure(EntityTypeBuilder<MembroDeEquipe> builder)
    {
        builder.ToTable("MembrosDeEquipe");

        builder.HasKey(m => m.TecnicoId);

        builder.Property(m => m.TecnicoId)
            .ValueGeneratedNever();

        builder.Property(m => m.EquipeId)
            .IsRequired();

        builder.HasIndex(m => m.EquipeId);

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
