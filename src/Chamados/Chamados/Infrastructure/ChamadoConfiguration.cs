using Chamados.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chamados.Infrastructure;

/// <summary>
/// Mapeamento EF Core de Chamado (Chamados.Domain). Construtor privado + todas as
/// propriedades com setter privado ou sem setter: EF Core precisa acessar os backing fields
/// diretamente, não as propriedades (arquitetura/02-dominio-hibrido.md).
/// UsePropertyAccessMode(PropertyAccessMode.Field) no nível da entidade cobre todas de uma vez.
///
/// RowVersion: shadow property byte[] com IsRowVersion() (concorrência otimista, obrigatória
/// por plano-de-arquitetura.md secao 5 — AtribuirChamadoCommand é o ponto de disputa concreta
/// descrito, mas o shadow property protege o agregado inteiro contra qualquer gravação
/// concorrente, não só a atribuição). Shadow property porque Chamado.cs (Domain) não tem, nem
/// deveria ter, uma propriedade RowVersion — concorrência otimista é preocupação de
/// persistência, não de domínio (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).
/// </summary>
internal sealed class ChamadoConfiguration : IEntityTypeConfiguration<Chamado>
{
    public void Configure(EntityTypeBuilder<Chamado> builder)
    {
        builder.ToTable("Chamados");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.SolicitanteId)
            .IsRequired();

        builder.Property(c => c.CategoriaId)
            .IsRequired();

        builder.Property(c => c.EquipeId)
            .IsRequired();

        builder.Property(c => c.Prioridade)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.AbertoEm)
            .IsRequired();

        builder.Property(c => c.PrazoSla)
            .IsRequired();

        builder.Property(c => c.TecnicoAtribuidoId);

        builder.Property(c => c.NotaResolucao)
            .HasMaxLength(4000);

        builder.Property(c => c.ResolvidoEm);

        builder.Property(c => c.FechadoEm);

        builder.Property(c => c.Escalonado)
            .IsRequired();

        builder.Property(c => c.DataEscalonamento);

        // Shadow property: concorrência otimista (plano-de-arquitetura.md secao 5, "Concorrência
        // otimista"; SharedKernel/ConcurrencyException.cs). Nunca exposta como propriedade em
        // Chamados.Domain.Chamado.
        //
        // IsRowVersion() foi tentado primeiro (API mais direta e portável para SQL Server,
        // 10-configuracao-e-segredos.md/plano-de-arquitetura.md secao 8 assumiam SQL Server como
        // alvo final) e descartado: confirmado com projeto isolado reproduzindo o mesmo
        // provider/versão desta solution que, no Sqlite, IsRowVersion() nunca gera valor nenhum
        // — a coluna fica NULL para sempre, tanto no INSERT quanto no UPDATE, quebrando a
        // concorrência otimista para 100% das gravações (WHERE RowVersion = @valor nunca é
        // verdadeiro contra uma coluna NULL). IsConcurrencyToken() (sem ValueGenerated) + o valor
        // atribuído por RowVersionInterceptor (Infrastructure/RowVersionInterceptor.cs) a cada
        // SaveChanges é a alternativa que este mesmo achado já antecipava.
        builder.Property<byte[]>("RowVersion")
            .IsConcurrencyToken();

        builder.HasIndex(c => c.SolicitanteId);
        builder.HasIndex(c => new { c.EquipeId, c.Status });

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
