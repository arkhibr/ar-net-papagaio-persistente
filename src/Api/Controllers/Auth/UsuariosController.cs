using Api.Bff;
using Api.Infrastructure;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Auth;

/// <summary>
/// Diretório de usuários do BFF (/api/v1/usuarios), só para UX: trocar GUID por nome nas telas e
/// escolher membros de equipe na administração do Catálogo. Os dados são da borda (BffDbContext),
/// não de um módulo de negócio, por isso não passam por ISender.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/usuarios")]
public sealed class UsuariosController : ControllerBase
{
    /// <summary>GET — todos os usuários com login e papéis. Só Supervisor.</summary>
    [HttpGet]
    [Authorize(Roles = "Supervisor")]
    public async Task<IActionResult> ListarAsync(CancellationToken cancellationToken) =>
        Ok(await Diretorio().ListarAsync(cancellationToken));

    /// <summary>
    /// GET nomes?ids=a&amp;ids=b — nomes de exibição dos ids pedidos (até 100), para qualquer
    /// autenticado. Não lista ninguém que o cliente não tenha pedido.
    /// </summary>
    [HttpGet("nomes")]
    [Authorize]
    public async Task<IActionResult> NomesAsync([FromQuery] Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length > DiretorioDeUsuarios.MaximoDeIdsPorConsulta)
        {
            return new ProblemResult(HttpErrors.Validation(
            [
                new HttpErrors.ErroDeCampo("ids", "tamanho_invalido",
                    $"Peça no máximo {DiretorioDeUsuarios.MaximoDeIdsPorConsulta} ids por vez."),
            ]));
        }

        return Ok(await Diretorio().NomesAsync(ids, cancellationToken));
    }

    private DiretorioDeUsuarios Diretorio() => HttpContext.RequestServices.GetRequiredService<DiretorioDeUsuarios>();
}
