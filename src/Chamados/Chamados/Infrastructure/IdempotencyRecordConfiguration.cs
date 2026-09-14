using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infrastructure;

/// <summary>
/// Mapeamento EF Core de IdempotencyRecordEntity. Índice único em IdempotencyKey: é o que
/// torna ReserveAsync atômico sob concorrência real (duas requisições inserindo a mesma chave
/// ao mesmo tempo — só uma vence, a outra recebe violação de unicidade do banco).
/// </summary>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordEntity> builder)
    {
        builder.ToTable("ChamadosIdempotencyRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(r => r.IdempotencyKey)
            .IsUnique();

        builder.Property(r => r.IsCompleted)
            .IsRequired();

        builder.Property(r => r.SerializedResponse);

        builder.Property(r => r.CriadoEm)
            .IsRequired();

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
