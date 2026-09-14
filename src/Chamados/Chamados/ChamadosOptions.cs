using System.ComponentModel.DataAnnotations;

namespace Chamados;

/// <summary>
/// Opções fortemente tipadas do módulo Chamados (arquitetura/10-configuracao-e-segredos.md).
/// Ver nota equivalente em Catalogo/CatalogoOptions.cs. Lida só pela composição raiz
/// (Api/Worker) e por ChamadosDependencyInjection — nunca por Domain/Application.
/// </summary>
public sealed class ChamadosOptions
{
    public const string SectionName = "Chamados";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;
}
