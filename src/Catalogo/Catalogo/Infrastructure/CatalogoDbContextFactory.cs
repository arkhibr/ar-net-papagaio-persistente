using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalogo.Infrastructure;

/// <summary>
/// Factory de design-time para "dotnet ef migrations add"/"database update" rodando
/// diretamente contra este projeto, já que ainda não existe Api/Worker como composition root
/// para hospedar as migrations (api-e-composicao, próxima etapa do pipeline). Usa uma
/// connection string fixa de desenvolvimento local — nunca lida a partir de configuração
/// de produção (isso seria papel da composição raiz, arquitetura/10-configuracao-e-segredos.md,
/// que este factory explicitamente não é).
///
/// Nenhuma migration foi gerada nesta rodada (instrução explícita: não rodar dotnet ef/build/
/// restore neste ambiente — ver pacotes.md). Este arquivo fica pronto para "dotnet ef
/// migrations add" assim que alguém rodar com rede/cache confirmados.
/// </summary>
internal sealed class CatalogoDbContextFactory : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogoDbContext>();
        optionsBuilder.UseSqlite("Data Source=catalogo.db");

        return new CatalogoDbContext(optionsBuilder.Options);
    }
}
