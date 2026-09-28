using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalogo.Infrastructure;

/// <summary>
/// Mapeamento EF Core de CategoriaDeServico (Catalogo.Domain), entidade majoritariamente
/// anêmica (plano-de-arquitetura.md secao 2). Todas as propriedades têm setter privado e não
/// há construtor público sem parâmetro (só o factory Criar(...)) — EF Core precisa acessar o
/// backing field diretamente (arquitetura/02-dominio-hibrido.md: "auto-propriedade com setter
/// privado" é o padrão aceito para persistência via EF Core sem abrir mão do encapsulamento
/// do construtor). UsePropertyAccessMode(Field) no nível da entidade cobre as três
/// propriedades de uma vez, sem precisar de .HasField(...) individual por propriedade.
/// </summary>
internal sealed class CategoriaDeServicoConfiguration : IEntityTypeConfiguration<CategoriaDeServico>
{
    public void Configure(EntityTypeBuilder<CategoriaDeServico> builder)
    {
        builder.ToTable("CategoriasDeServico");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Nome)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.EquipeId)
            .IsRequired();

        // Nunca apagada, só inativada (B6 de achados.md). Padrão true para linhas antigas.
        builder.Property(c => c.Ativa)
            .IsRequired()
            .HasDefaultValue(true);

        // Tabela de referência de SLA por prioridade (M13 de achados.md). Chave (CategoriaId,
        // Prioridade): no máximo um SLA por prioridade em cada categoria.
        builder.OwnsMany(c => c.Slas, sla =>
        {
            sla.ToTable("SlasDeCategoria");
            sla.WithOwner().HasForeignKey("CategoriaId");
            sla.Property<Guid>("CategoriaId");
            sla.HasKey("CategoriaId", nameof(SlaDaCategoria.Prioridade));
            sla.Property(s => s.Prioridade).HasConversion<int>();
            sla.Property(s => s.Horas).IsRequired();
        });
        builder.Navigation(c => c.Slas).HasField("_slas");

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
