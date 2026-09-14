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

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
