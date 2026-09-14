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
        // Nota de risco não verificado (sem compilador/banco disponível nesta sessão, ver
        // pacotes.md): IsRowVersion() é a API que o SQL Server mapeia diretamente para uma
        // coluna nativa `rowversion`/`timestamp`, auto-gerada pelo próprio motor a cada UPDATE.
        // O provider Sqlite não tem um tipo nativo equivalente; o EF Core moderno (Sqlite
        // provider) simula IsRowVersion() gerando o valor em memória (um novo valor a cada
        // SaveChanges, não pelo banco), o que ainda cumpre o contrato de concorrência otimista
        // (o WHERE da query de UPDATE compara o valor original, ainda detecta conflito), mas o
        // comportamento exato não foi confirmado por build/teste real nesta sessão. Se
        // Infrastructure.IntegrationTests (construcao-de-testes) revelar que IsRowVersion()
        // não gera um novo valor a cada update no provider Sqlite, a alternativa é
        // IsConcurrencyToken() combinado com um ValueGenerator explícito (ex.: Guid.NewGuid()
        // a cada update via um SaveChangesInterceptor), mantendo IsRowVersion() como a primeira
        // tentativa por ser a API mais direta e portável para SQL Server (10-configuracao-e-
        // segredos.md/plano-de-arquitetura.md secao 8 assumiam SQL Server como alvo final).
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        builder.HasIndex(c => c.SolicitanteId);
        builder.HasIndex(c => new { c.EquipeId, c.Status });

        builder.UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
