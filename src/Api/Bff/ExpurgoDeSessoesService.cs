using Microsoft.Extensions.Options;

namespace Api.Bff;

/// <summary>
/// Expurgo periódico das AuthSessions encerradas, para a tabela não crescer sem limite. Roda na
/// própria Api (não há Worker ainda). A primeira execução só acontece depois de um intervalo, e
/// uma falha é logada sem derrubar o processo.
/// </summary>
internal sealed class ExpurgoDeSessoesService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SessaoOptions _options;
    private readonly ILogger<ExpurgoDeSessoesService> _logger;

    public ExpurgoDeSessoesService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<SessaoOptions> options,
        ILogger<ExpurgoDeSessoesService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.IntervaloDoExpurgo, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var removidas = await scope.ServiceProvider.GetRequiredService<SessoesDeAutenticacao>().ExpurgarAsync(stoppingToken);
                if (removidas > 0)
                {
                    _logger.LogInformation("Expurgo removeu {Quantidade} sessões encerradas", removidas);
                }
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                _logger.LogWarning(erro, "Falha no expurgo de sessões; nova tentativa no próximo intervalo");
            }
        }
    }
}
