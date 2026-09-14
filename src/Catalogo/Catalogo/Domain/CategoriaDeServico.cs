using SharedKernel;

namespace Catalogo.Domain;

/// <summary>
/// Entidade majoritariamente anêmica do módulo Catalogo. Único invariante: toda categoria
/// precisa de uma equipe responsável definida na criação (ver plano-de-arquitetura.md,
/// secao 2, e especificacao-clarificada.md). Não atinge o sinal de promoção para rico.
/// </summary>
internal sealed class CategoriaDeServico
{
    public Guid Id { get; }
    public string Nome { get; }
    public Guid EquipeId { get; }

    private CategoriaDeServico(Guid id, string nome, Guid equipeId)
    {
        Id = id;
        Nome = nome;
        EquipeId = equipeId;
    }

    public static CategoriaDeServico Criar(string nome, Guid equipeId)
    {
        if (equipeId == Guid.Empty)
        {
            throw new DomainException("Toda categoria de serviço precisa de uma equipe responsável.");
        }

        return new CategoriaDeServico(Guid.NewGuid(), nome, equipeId);
    }
}
