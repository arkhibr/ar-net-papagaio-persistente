namespace SharedKernel;

/// <summary>
/// Chaves estáveis usadas para registrar o IMediator/ISender gerado por cada módulo como
/// serviço "keyed" (AddKeyedScoped, .NET 8+), consumido pelo composite ISender da Api (ver
/// Api/Infrastructure/CompositeSender.cs). Fica em SharedKernel porque tanto cada módulo
/// (que registra a própria chave em Add{Modulo}Module) quanto a Api (que resolve todas as
/// chaves para montar o composite) precisam do mesmo literal — SharedKernel é o único projeto
/// que os dois lados já referenciam.
/// </summary>
public static class ModuleSenderKeys
{
    public const string Chamados = "Chamados";
    public const string Catalogo = "Catalogo";
}
