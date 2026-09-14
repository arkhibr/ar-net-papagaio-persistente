using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Abre um novo chamado. Autorização: papel "solicitante autenticado" — leitura A, genérica,
/// resolvida no controller ([Authorize] simples), por isso NÃO implementa
/// IRequiresAuthorization (plano-de-arquitetura.md secao 5). Exige Idempotency-Key (a mesma
/// tentativa de abertura reenviada pelo cliente não deve criar dois chamados).
///
/// SolicitanteId chega no Command resolvido do ICurrentUser pelo controller (nunca confiado
/// vindo do corpo da requisição, arquitetura/11-autenticacao-e-sessao.md) — aqui, na Application,
/// é só um dado de entrada como outro qualquer.
///
/// Prioridade é escolhida explicitamente pelo solicitante na abertura (correção registrada em
/// especificacao-clarificada.md, "Quem define prioridade") — o handler repassa esse valor para
/// Catalogo.Contracts.ResolverEquipeESlaQuery, nunca um valor fixo hardcoded.
///
/// Tipo público em Contracts (não interno em Application): tem rota HTTP, e a Api só pode
/// referenciar Contracts (arquitetura/01-estrutura-de-projetos-monolito-modular.md).
///
/// IAuditable: abertura está no subconjunto auditado (plano-de-arquitetura.md secao 7;
/// especificacao-clarificada.md).
/// </summary>
public sealed record AbrirChamadoCommand(
    Guid SolicitanteId,
    Guid CategoriaId,
    PrioridadeChamado Prioridade,
    string IdempotencyKey)
    : IRequest<Result<Guid>>, IIdempotentCommand, IAuditable, ITransactionalCommand;
