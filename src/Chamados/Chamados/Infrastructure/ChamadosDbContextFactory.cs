using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chamados.Infrastructure;

/// <summary>
/// Factory de design-time para "dotnet ef migrations add"/"database update" rodando
/// diretamente contra este projeto, já que ainda não existe Api/Worker como composition root
/// para hospedar as migrations (api-e-composicao, próxima etapa do pipeline). Connection
/// string fixa de desenvolvimento local — nunca lida de configuração de produção (ver nota
/// equivalente em Catalogo/Infrastructure/CatalogoDbContextFactory.cs).
///
/// Nenhuma migration foi gerada nesta rodada (instrução explícita: não rodar dotnet ef/build/
/// restore neste ambiente — ver pacotes.md). Este arquivo fica pronto para "dotnet ef
/// migrations add" assim que alguém rodar com rede/cache confirmados.
/// </summary>
internal sealed class ChamadosDbContextFactory : IDesignTimeDbContextFactory<ChamadosDbContext>
{
    public ChamadosDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChamadosDbContext>();
        optionsBuilder.UseSqlite("Data Source=chamados.db");

        return new ChamadosDbContext(optionsBuilder.Options);
    }
}
