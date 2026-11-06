using Mediator;
using Microsoft.Extensions.DependencyInjection;

// Handlers deste módulo dependem de serviços Scoped (ChamadosDbContext via IChamadoRepository,
// ICurrentUser, IUnitOfWork, IIdempotencyStore, IEquipeDoUsuarioResolver,
// IChamadosAuthorizationContext — todos Scoped, keyed ou não, em ChamadosDependencyInjection). O default
// do Mediator.SourceGenerator é ServiceLifetime.Singleton (melhor throughput quando não há
// dependência Scoped) — errado aqui: um handler Singleton prendendo uma dependência Scoped no
// construtor (captive dependency) resolveria sempre a MESMA instância de ChamadosDbContext/
// ICurrentUser entre requisições diferentes, quebrando isolamento por requisição. Por isso este
// módulo declara o lifetime como Scoped explicitamente — decisão de compile-time (o gerador só
// aceita este ajuste via atributo de assembly ou pela própria chamada de AddMediator(options),
// e o próprio Mediator.g.cs lança em runtime se o lifetime pedido via AddMediator(options) não
// bater com o que o gerador "viu" em tempo de compilação — por isso o atributo, não só o
// parâmetro em AddMediator, é a fonte da verdade aqui).
[assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped)]
