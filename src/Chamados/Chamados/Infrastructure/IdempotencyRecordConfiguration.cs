using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infrastructure;

/// <summary>
/// Mapeamento de IdempotencyRecordEntity. O índice único em (Scope, IdempotencyKey) é o que
/// torna a reserva atômica sob concorrência: só uma inserção vence; a outra recebe violação de
/// unicidade, que IdempotencyStore.ReserveAsync traduz para OperationInProgressException (409).
/// </summary>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecordEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordEntity> builder)
    {
        builder.ToTable("ChamadosIdempotencyRecords");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Scope)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(r => new { r.Scope, r.IdempotencyKey })
            .IsUnique();

        builder.Property(r => r.PayloadHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.IsCompleted)
            .IsRequired();

        builder.Property(r => r.SerializedResponse);

        builder.Property(r => r.ReservedAt)
            .IsRequired();

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
