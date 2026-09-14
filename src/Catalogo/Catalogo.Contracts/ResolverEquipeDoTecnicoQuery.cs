using Mediator;
using SharedKernel;

namespace Catalogo.Contracts;

/// <summary>
/// Query pública de Catalogo, consultada pela implementação real de IEquipeDoUsuarioResolver
/// (Chamados.Application), usada por FilaDaEquipeQueryHandler para resolver a EquipeId do
/// técnico/supervisor autenticado (filtro por linha, arquitetura/14-filtro-de-dados.md) —
/// nunca aceita EquipeId vinda do cliente. Resolve a equipe única à qual o técnico pertence;
/// Value é null se o técnico não estiver vinculado a nenhuma equipe (não é falha de negócio,
/// é ausência de vínculo — quem decide o que fazer com isso é o handler chamador).
/// </summary>
public sealed record ResolverEquipeDoTecnicoQuery(Guid TecnicoId) : IRequest<Result<Guid?>>;
