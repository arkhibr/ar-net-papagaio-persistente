namespace Api.IntegrationTests;

/// <summary>
/// Dado de referência mínimo no Catálogo (categoria com equipe, técnico membro da equipe),
/// inserido direto no SQLite porque o Catálogo não expõe Command de cadastro.
/// </summary>
public sealed class Cenario
{
    public Guid CategoriaId { get; } = Guid.NewGuid();
    public Guid EquipeId { get; } = Guid.NewGuid();
    public Guid TecnicoId { get; } = Guid.NewGuid();
    public Guid OutroTecnicoId { get; } = Guid.NewGuid();

    public static async Task<Cenario> CriarAsync(ApiFactory factory)
    {
        // Garante que a Api subiu (e que IDatabaseInitializer criou o schema) antes de inserir.
        using (var cliente = factory.CreateClient())
        {
            await cliente.GetAsync("/health");
        }

        var cenario = new Cenario();
        Seed.InserirCategoria(factory, cenario.CategoriaId, cenario.EquipeId);
        Seed.InserirMembro(factory, cenario.TecnicoId, cenario.EquipeId);
        Seed.InserirMembro(factory, cenario.OutroTecnicoId, cenario.EquipeId);
        return cenario;
    }
}

public static class Seed
{
    /// <summary>Categoria com SLA para as quatro prioridades (0=Baixa 72h, 1=Media 24h, 2=Alta 8h, 3=Critica 4h).</summary>
    public static void InserirCategoria(ApiFactory factory, Guid categoriaId, Guid equipeId, bool comSla = true)
    {
        var id = categoriaId.ToString().ToUpperInvariant();
        factory.ExecutarSql(
            factory.CatalogoDb,
            "INSERT INTO CategoriasDeServico (Id, Nome, EquipeId) VALUES ($id, 'Rede', $equipe)",
            ("$id", id),
            ("$equipe", equipeId.ToString().ToUpperInvariant()));

        if (comSla)
        {
            factory.ExecutarSql(
                factory.CatalogoDb,
                "INSERT INTO SlasDeCategoria (CategoriaId, Prioridade, Horas) VALUES ($id, 0, 72), ($id, 1, 24), ($id, 2, 8), ($id, 3, 4)",
                ("$id", id));
        }
    }

    public static void InserirMembro(ApiFactory factory, Guid tecnicoId, Guid equipeId) =>
        factory.ExecutarSql(
            factory.CatalogoDb,
            "INSERT INTO MembrosDeEquipe (TecnicoId, EquipeId) VALUES ($tecnico, $equipe)",
            ("$tecnico", tecnicoId.ToString().ToUpperInvariant()),
            ("$equipe", equipeId.ToString().ToUpperInvariant()));
}
