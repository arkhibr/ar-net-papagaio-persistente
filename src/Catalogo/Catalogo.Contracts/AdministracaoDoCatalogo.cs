using Mediator;
using SharedKernel;

namespace Catalogo.Contracts;

// Administração do Catálogo: categorias, SLAs, equipes e membros. Autorização por papel
// (Supervisor) em dois pontos: [Authorize(Roles)] na Api (leitura A, arquitetura/12) e
// IRequerAdministradorDoCatalogo na própria mensagem, que vale para qualquer chamador de
// Contracts. Sem Idempotency-Key:
// nenhuma destas ações tem efeito externo ou crítico (critério de arquitetura/09); as ações
// sobre um recurso existente são idempotentes por natureza, e reenviar uma criação só gera um
// registro a mais, que o supervisor inativa. Cada mudança é uma ação nomeada, nunca um PUT
// genérico (arquitetura/09, "Sem PUT/PATCH genérico").

/// <summary>Cria uma categoria já com a tabela de SLA. Devolve o id.</summary>
public sealed record CriarCategoriaDeServicoCommand(string Nome, Guid EquipeId, IReadOnlyList<SlaDto> Slas)
    : IRequest<Result<Guid>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

public sealed record RenomearCategoriaDeServicoCommand(Guid CategoriaId, string Nome)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>Muda a equipe responsável. Chamados já abertos mantêm a equipe do snapshot.</summary>
public sealed record TransferirCategoriaDeServicoCommand(Guid CategoriaId, Guid EquipeId)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>Substitui a tabela de SLA inteira. Prioridade ausente deixa de ser atendida.</summary>
public sealed record DefinirSlasDaCategoriaCommand(Guid CategoriaId, IReadOnlyList<SlaDto> Slas)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>A categoria deixa de aceitar chamado novo; os antigos continuam (nunca é apagada).</summary>
public sealed record InativarCategoriaDeServicoCommand(Guid CategoriaId)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

public sealed record ReativarCategoriaDeServicoCommand(Guid CategoriaId)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

public sealed record CriarEquipeCommand(string Nome) : IRequest<Result<Guid>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

public sealed record RenomearEquipeCommand(Guid EquipeId, string Nome) : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>
/// Vincula um usuário (técnico ou supervisor) à equipe. Um usuário pertence a no máximo uma
/// equipe: se já estiver em outra, é falha de negócio (desvincule antes). Vincular de novo à
/// mesma equipe não muda nada.
/// </summary>
public sealed record VincularMembroDaEquipeCommand(Guid EquipeId, Guid UsuarioId)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>Remove o vínculo. 404 se o usuário não for membro desta equipe.</summary>
public sealed record DesvincularMembroDaEquipeCommand(Guid EquipeId, Guid UsuarioId)
    : IRequest<Result<Unit>>, ITransactionalCommand, IRequerAdministradorDoCatalogo;

/// <summary>Equipes com os membros. Para a administração (Supervisor).</summary>
public sealed record EquipesQuery : IRequest<Result<IReadOnlyList<EquipeDto>>>, IRequerAdministradorDoCatalogo;

public sealed record EquipeDto(Guid Id, string Nome, IReadOnlyList<Guid> Membros);
