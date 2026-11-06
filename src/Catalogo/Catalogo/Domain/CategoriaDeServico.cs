using Catalogo.Contracts;
using SharedKernel;

namespace Catalogo.Domain;

/// <summary>
/// Categoria de serviço com a equipe responsável e o SLA de cada prioridade
/// (especificacao-clarificada.md: "CategoriaDeServico guarda SLA por prioridade"; M13 de
/// achados.md). O SLA é dado de referência do Catálogo, não regra fixa em código.
/// Invariantes: nome obrigatório (até 200); equipe responsável obrigatória; no máximo um SLA por
/// prioridade; horas > 0.
///
/// Uma categoria nunca é apagada, só inativada (B6 de achados.md): chamados antigos continuam
/// apontando para ela, e a categoria inativa só deixa de aceitar chamado novo.
/// </summary>
internal sealed class CategoriaDeServico
{
    public const int TamanhoMaximoDoNome = 200;

    private readonly List<SlaDaCategoria> _slas = [];

    public Guid Id { get; }
    public string Nome { get; private set; }
    public Guid EquipeId { get; private set; }
    public bool Ativa { get; private set; }
    public IReadOnlyCollection<SlaDaCategoria> Slas => _slas;

    private CategoriaDeServico(Guid id, string nome, Guid equipeId)
    {
        Id = id;
        Nome = nome;
        EquipeId = equipeId;
        Ativa = true;
    }

    public static CategoriaDeServico Criar(
        string nome, Guid equipeId, IEnumerable<(PrioridadeServico Prioridade, int Horas)> slas) =>
        Criar(Guid.NewGuid(), nome, equipeId, slas);

    /// <summary>Id gerado pela Application (M12 de achados.md: o Domain não gera aleatoriedade).</summary>
    public static CategoriaDeServico Criar(
        Guid id, string nome, Guid equipeId, IEnumerable<(PrioridadeServico Prioridade, int Horas)> slas)
    {
        var categoria = new CategoriaDeServico(id, NomeValido(nome), EquipeValida(equipeId));

        foreach (var (prioridade, horas) in slas)
        {
            categoria.DefinirSla(prioridade, horas);
        }

        return categoria;
    }

    public void Renomear(string nome) => Nome = NomeValido(nome);

    /// <summary>Muda a equipe responsável. Chamados já abertos mantêm o snapshot da equipe antiga.</summary>
    public void TransferirPara(Guid equipeId) => EquipeId = EquipeValida(equipeId);

    public void DefinirSla(PrioridadeServico prioridade, int horas)
    {
        if (horas <= 0)
        {
            throw new DomainException("O SLA de uma prioridade precisa ser de pelo menos uma hora.");
        }

        _slas.RemoveAll(sla => sla.Prioridade == prioridade);
        _slas.Add(new SlaDaCategoria(prioridade, horas));
    }

    /// <summary>
    /// Substitui a tabela de SLA inteira. Prioridade fora da lista deixa de ser atendida pela
    /// categoria (abrir chamado nela responde 400). Repetir a mesma prioridade é erro.
    /// </summary>
    public void RedefinirSlas(IEnumerable<(PrioridadeServico Prioridade, int Horas)> slas)
    {
        var novos = slas.ToList();
        if (novos.Select(s => s.Prioridade).Distinct().Count() != novos.Count)
        {
            throw new DomainException("Cada prioridade pode ter um único SLA.");
        }

        if (novos.Any(s => s.Horas <= 0))
        {
            throw new DomainException("O SLA de uma prioridade precisa ser de pelo menos uma hora.");
        }

        _slas.Clear();
        _slas.AddRange(novos.Select(s => new SlaDaCategoria(s.Prioridade, s.Horas)));
    }

    /// <summary>Idempotente: inativar uma categoria já inativa não muda nada.</summary>
    public void Inativar() => Ativa = false;

    /// <summary>Idempotente: reativar uma categoria ativa não muda nada.</summary>
    public void Reativar() => Ativa = true;

    public int? HorasDeSlaPara(PrioridadeServico prioridade) =>
        _slas.FirstOrDefault(sla => sla.Prioridade == prioridade)?.Horas;

    private static string NomeValido(string nome)
    {
        var limpo = nome?.Trim() ?? string.Empty;
        if (limpo.Length == 0 || limpo.Length > TamanhoMaximoDoNome)
        {
            throw new DomainException($"O nome da categoria é obrigatório e tem até {TamanhoMaximoDoNome} caracteres.");
        }

        return limpo;
    }

    private static Guid EquipeValida(Guid equipeId) =>
        equipeId == Guid.Empty
            ? throw new DomainException("Toda categoria de serviço precisa de uma equipe responsável.")
            : equipeId;
}

/// <summary>SLA de uma prioridade dentro de uma categoria (tabela de referência SlasDeCategoria).</summary>
internal sealed record SlaDaCategoria(PrioridadeServico Prioridade, int Horas);
