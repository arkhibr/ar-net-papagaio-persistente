using Catalogo.Contracts;
using Chamados.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real de IAuthorizationContext (SharedKernel) para o módulo Chamados.
/// Consultada pelo AuthorizationBehavior (SharedKernel) a partir de
/// AtribuirChamadoCommand/DevolverChamadoCommand/ResolverChamadoCommand.IsAuthorizedAsync,
/// todos chamando HasResourceLinkAsync(currentUser.UserId, ChamadoId, ...) com o mesmo
/// método, mas cada Command aplicando um sentido diferente de "vínculo" ao resultado — por
/// isso a implementação precisa cobrir os dois sentidos numa única resposta coerente:
///
/// - DevolverChamadoCommand/ResolverChamadoCommand: "vínculo" = usuário é o técnico
///   atualmente atribuído ao chamado (só possível quando o chamado já está EmAtendimento —
///   estado que o próprio agregado exige depois, então o segundo braço abaixo nunca se aplica
///   nestes dois casos).
/// - AtribuirChamadoCommand: "vínculo" = chamado ainda está Aberto (ninguém atribuído) e o
///   usuário é membro da equipe responsável pelo chamado (checagem que atravessa
///   Catalogo.Contracts, dado do módulo Catalogo — plano-de-arquitetura.md secao 5, nota;
///   arquitetura/04-comunicacao-entre-modulos.md, nunca acesso direto ao schema de Catalogo).
///
/// A união dos dois braços (TecnicoAtribuidoId == userId) OR (Status == Aberto AND membro da
/// equipe) responde corretamente às duas perguntas com a mesma assinatura, porque cada Command
/// só é despachado num estado em que só um dos dois braços pode ser verdadeiro (autorização
/// ainda roda antes do handler carregar/validar o estado do agregado, mas os dois braços não
/// se sobrepõem na prática: Atribuir exige Aberto pelo próprio invariante do agregado,
/// Devolver/Resolver exigem EmAtendimento).
///
/// Leitura não rastreada: só consulta, nunca altera o Chamado (arquitetura/27).
/// </summary>
internal sealed class ChamadoAuthorizationContext : IAuthorizationContext
{
    private readonly ChamadosDbContext _dbContext;
    private readonly ISender _sender;

    public ChamadoAuthorizationContext(ChamadosDbContext dbContext, ISender sender)
    {
        _dbContext = dbContext;
        _sender = sender;
    }

    public async Task<bool> HasResourceLinkAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken)
    {
        var chamado = await _dbContext.Chamados
            .AsNoTracking()
            .Where(c => c.Id == resourceId)
            .Select(c => new { c.Status, c.EquipeId, c.TecnicoAtribuidoId })
            .FirstOrDefaultAsync(cancellationToken);

        if (chamado is null)
        {
            return false;
        }

        if (chamado.TecnicoAtribuidoId == userId)
        {
            return true;
        }

        if (chamado.Status != StatusChamado.Aberto)
        {
            return false;
        }

        var resultado = await _sender.Send(
            new EhMembroDaEquipeQuery(userId, chamado.EquipeId), cancellationToken);

        return resultado.Value;
    }
}
