using SharedKernel;

namespace Catalogo.Domain;

/// <summary>
/// Equipe de atendimento. Antes era só um EquipeId solto nas categorias e nos vínculos de membro;
/// passa a ter nome para ser exibida e administrada. O vínculo de membro continua como dado plano
/// (MembroDeEquipe, na Infrastructure). Invariante: nome obrigatório, até 100 caracteres.
/// </summary>
internal sealed class Equipe
{
    public const int TamanhoMaximoDoNome = 100;

    public Guid Id { get; }
    public string Nome { get; private set; }

    private Equipe(Guid id, string nome)
    {
        Id = id;
        Nome = nome;
    }

    /// <summary>Id gerado pela Application (M12 de achados.md).</summary>
    public static Equipe Criar(Guid id, string nome)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A equipe precisa de um identificador.");
        }

        return new Equipe(id, NomeValido(nome));
    }

    public void Renomear(string nome) => Nome = NomeValido(nome);

    private static string NomeValido(string nome)
    {
        var limpo = nome?.Trim() ?? string.Empty;
        if (limpo.Length == 0 || limpo.Length > TamanhoMaximoDoNome)
        {
            throw new DomainException($"O nome da equipe é obrigatório e tem até {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }
}
