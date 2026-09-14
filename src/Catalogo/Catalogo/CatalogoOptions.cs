using System.ComponentModel.DataAnnotations;

namespace Catalogo;

/// <summary>
/// Opções fortemente tipadas do módulo Catalogo (arquitetura/10-configuracao-e-segredos.md):
/// configuração usada em mais de um lugar (aqui, só a connection string do próprio módulo,
/// mas já nasce como IOptions&lt;T&gt; para não precisar de migração depois se um segundo valor
/// relacionado aparecer) vira classe de opções validada na inicialização
/// (.ValidateDataAnnotations().ValidateOnStart(), registrado em CatalogoDependencyInjection).
/// Lida só pela composição raiz (Api/Worker) e por CatalogoDependencyInjection — nunca por
/// Domain/Application (Domain nunca lê configuração).
/// </summary>
public sealed class CatalogoOptions
{
    public const string SectionName = "Catalogo";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;
}
