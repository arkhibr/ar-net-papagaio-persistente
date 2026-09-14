namespace SharedKernel;

/// <summary>
/// Porta estreita para a composição raiz (Api/Worker) inicializar o schema do banco de um
/// módulo sem conhecer o DbContext concreto (que é internal ao módulo, arquitetura/01). Cada
/// módulo registra a própria implementação, do mesmo jeito que já faz para IUnitOfWork/health
/// check — a Api só resolve IEnumerable&lt;IDatabaseInitializer&gt; e chama, sem saber quantos
/// módulos existem nem como cada um persiste.
///
/// EnsureCreatedAsync é ferramenta de desenvolvimento/teste local (cria o schema a partir do
/// modelo atual, sem histórico de migration) — nunca chamado em produção, onde o caminho
/// correto é migration real (dotnet ef migrations, ainda não gerada nesta solution, ver
/// pacotes.md/testes-manuais.md). Program.cs só invoca isto quando
/// IHostEnvironment.IsDevelopment().
/// </summary>
public interface IDatabaseInitializer
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken);
}
