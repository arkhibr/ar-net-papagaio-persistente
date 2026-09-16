using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chamados.Infrastructure;

/// <summary>
/// Achado real (não simulado por leitura de código, confirmado com projeto isolado
/// reproduzindo o mesmo provider/versão desta solution): <c>IsRowVersion()</c> (usado antes em
/// ChamadoConfiguration) nunca gera valor nenhum no provider Sqlite — a coluna fica NULL para
/// sempre, tanto no INSERT quanto no UPDATE, quebrando a concorrência otimista para 100% das
/// gravações (toda comparação subsequente é contra NULL, nunca verdadeira). Tampouco resolve
/// configurar <c>ValueGeneratedOnAddOrUpdate()</c> + <c>HasValueGenerator&lt;T&gt;()</c>: o EF
/// Core marca a propriedade como "BeforeSave:Ignore" nesse caso (assume que o próprio banco vai
/// gerar o valor via um DEFAULT/trigger, o que o Sqlite não tem), e por isso a coluna nem entra
/// na lista de colunas do INSERT/UPDATE gerado.
///
/// Correção: interceptor que atribui o valor diretamente, como uma propriedade comum, ANTES do
/// SaveChanges normal do EF Core processar a entidade — nesse caminho a propriedade é enviada de
/// verdade no INSERT/UPDATE (ValueGenerated permanece "Never" em ChamadoConfiguration, o que é
/// literalmente correto: o valor sempre vem de código de aplicação, nunca do banco).
///
/// Roda para toda entidade rastreada com uma shadow property "RowVersion" (não só Chamado) —
/// generaliza automaticamente se outra entidade do módulo ganhar o mesmo padrão de concorrência
/// otimista depois, sem precisar editar este interceptor.
/// </summary>
internal sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        AtribuirNovosRowVersions(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AtribuirNovosRowVersions(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AtribuirNovosRowVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Metadata.FindProperty("RowVersion") is null)
            {
                continue;
            }

            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }
}
