using System.Net;
using Catalogo.Contracts;
using Chamados.Contracts;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using SharedKernel.Modules;

namespace Api.IntegrationTests;

/// <summary>
/// Achados A2, A5, C3/P1 (composição), M4, M10, M11 e M13 de achados.md, pela Api real.
/// </summary>
public sealed class RegrasDoModuloTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> EtagAsync(HttpClient cliente, Guid chamadoId)
    {
        var resposta = await cliente.GetAsync($"/api/v1/chamados/{chamadoId}");
        resposta.EnsureSuccessStatusCode();
        return resposta.Headers.ETag!.Tag;
    }

    private async Task<(HttpClient Tecnico, Guid ChamadoId)> ChamadoAtribuidoAsync()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        var tecnico = await _factory.ClienteAutenticadoAsync(_cenario.TecnicoId, "Tecnico");
        (await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/atribuir", null, Guid.NewGuid().ToString(), await EtagAsync(tecnico, chamadoId))))
            .EnsureSuccessStatusCode();
        return (tecnico, chamadoId);
    }

    [Fact]
    public void C3_P1_container_real_registra_validadores_e_uma_porta_por_modulo()
    {
        using var scope = _factory.Services.CreateScope();
        var servicos = scope.ServiceProvider;

        Assert.NotEmpty(servicos.GetServices<IValidator<AbrirChamadoCommand>>());

        var unitsOfWork = servicos.GetRequiredService<IModuleService<IUnitOfWork>>();
        Assert.Equal("ChamadosUnitOfWork", unitsOfWork.For(typeof(AbrirChamadoCommand)).GetType().Name);
        Assert.Equal("CatalogoUnitOfWork", unitsOfWork.For(typeof(CategoriasDeServicoQuery)).GetType().Name);
    }

    [Fact]
    public async Task M11_query_do_Catalogo_e_roteada_ao_modulo_dono()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var resposta = await cliente.GetAsync("/api/v1/categorias-de-servico");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains(_cenario.CategoriaId.ToString(), await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A5_operacoes_auditadas_gravam_registro_com_o_ator_na_mesma_transacao()
    {
        var (tecnico, chamadoId) = await ChamadoAtribuidoAsync();
        (await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/resolver", new { notaResolucao = "Cabo trocado" }, Guid.NewGuid().ToString())))
            .EnsureSuccessStatusCode();

        var recurso = ("$id", (object)chamadoId.ToString().ToUpperInvariant());
        Assert.Equal(3, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosRegistrosAuditoria", "ResourceId = $id", recurso));
        Assert.Equal(1, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosRegistrosAuditoria",
            "ResourceId = $id AND Action = 'ChamadoAtribuido' AND ActorUserId = $ator",
            recurso, ("$ator", _cenario.TecnicoId.ToString().ToUpperInvariant())));
        Assert.Equal(1, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosRegistrosAuditoria",
            "ResourceId = $id AND Action = 'ChamadoResolvido' AND Reason = 'Cabo trocado'", recurso));
    }

    [Fact]
    public async Task A2_devolver_tira_o_tecnico_do_chamado()
    {
        var (tecnico, chamadoId) = await ChamadoAtribuidoAsync();

        (await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/devolver", null, Guid.NewGuid().ToString()))).EnsureSuccessStatusCode();

        var detalhe = await ChamadosHttp.JsonAsync(await tecnico.GetAsync($"/api/v1/chamados/{chamadoId}"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, detalhe.GetProperty("tecnicoAtribuidoId").ValueKind);
    }

    [Fact]
    public async Task M10_membro_da_equipe_que_nao_e_o_atribuido_nao_resolve_e_recebe_403()
    {
        var (_, chamadoId) = await ChamadoAtribuidoAsync();
        var outroTecnico = await _factory.ClienteAutenticadoAsync(_cenario.OutroTecnicoId, "Tecnico");

        var resposta = await outroTecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/resolver", new { notaResolucao = "x" }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task M4_reclassificacao_negada_nao_reserva_a_Idempotency_Key()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        var registrosAntes = _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosIdempotencyRecords");
        var estranho = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Tecnico");

        var resposta = await estranho.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/reclassificar", new { novaPrioridade = 3 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(registrosAntes, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosIdempotencyRecords"));
    }

    [Fact]
    public async Task M13_sla_vem_da_tabela_de_referencia_da_categoria()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId, prioridade: 3);

        var detalhe = await ChamadosHttp.JsonAsync(await solicitante.GetAsync($"/api/v1/chamados/{chamadoId}"));
        var abertoEm = detalhe.GetProperty("abertoEm").GetDateTimeOffset();
        var prazoSla = detalhe.GetProperty("prazoSla").GetDateTimeOffset();

        Assert.Equal(TimeSpan.FromHours(4), prazoSla - abertoEm);
    }

    [Fact]
    public async Task M13_categoria_sem_sla_para_a_prioridade_responde_400_de_regra_de_negocio()
    {
        var categoriaSemSla = Guid.NewGuid();
        Seed.InserirCategoria(_factory, categoriaSemSla, _cenario.EquipeId, comSla: false);
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var resposta = await solicitante.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = categoriaSemSla, prioridade = 1 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await ChamadosHttp.JsonAsync(resposta);
        Assert.Equal("erros/regra-de-negocio-violada", corpo.GetProperty("type").GetString());
    }
}
