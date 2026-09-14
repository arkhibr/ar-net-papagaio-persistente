namespace Catalogo.Infrastructure;

/// <summary>
/// Registro do vínculo técnico-equipe (dado plano de referência, sem invariante de negócio
/// própria — ver Application/IMembroDeEquipeRepository.cs). Modelado só na Infrastructure,
/// não em Catalogo.Domain: não é um agregado, é uma linha de tabela de associação que o EF
/// Core mapeia diretamente, sem passar por regra de domínio nenhuma (Catalogo permanece
/// majoritariamente anêmico, plano-de-arquitetura.md secao 2).
///
/// Decisão de modelagem: um técnico pertence no máximo a uma equipe (ver
/// IMembroDeEquipeRepository.ResolverEquipeIdAsync, que devolve Guid? único, não uma lista) —
/// por isso TecnicoId é chave primária da tabela, não parte de uma chave composta
/// (TecnicoId, EquipeId), que permitiria múltiplos vínculos por técnico.
/// </summary>
internal sealed class MembroDeEquipe
{
    public Guid TecnicoId { get; private set; }
    public Guid EquipeId { get; private set; }

    private MembroDeEquipe()
    {
    }

    public MembroDeEquipe(Guid tecnicoId, Guid equipeId)
    {
        TecnicoId = tecnicoId;
        EquipeId = equipeId;
    }
}
