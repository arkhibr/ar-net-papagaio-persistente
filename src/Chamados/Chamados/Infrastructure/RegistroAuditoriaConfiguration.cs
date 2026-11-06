using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infrastructure;

internal sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoriaEntity>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoriaEntity> builder)
    {
        builder.ToTable("ChamadosRegistrosAuditoria");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.OccurredAt).IsRequired();
        builder.Property(r => r.ActorUserId).IsRequired();
        builder.Property(r => r.SessionId).IsRequired();
        builder.Property(r => r.IsSystemActor).IsRequired();
        builder.Property(r => r.Action).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ResourceType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ResourceId).IsRequired();
        builder.Property(r => r.OldValue).HasMaxLength(4000);
        builder.Property(r => r.NewValue).HasMaxLength(4000);
        builder.Property(r => r.Reason).HasMaxLength(4000);
        builder.Property(r => r.CorrelationId).HasMaxLength(100);
        builder.Property(r => r.SourceIp).HasMaxLength(64);

        builder.HasIndex(r => new { r.ResourceType, r.ResourceId });

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
