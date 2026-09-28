using System.ComponentModel.DataAnnotations;

namespace Api.Bff;

/// <summary>
/// Store técnico da borda (BFF): sessão server-side, chaves de Data Protection e credencial local
/// (adaptacao-bff-angular.md, B6/B7/B10; decisão D3 = banco relacional). Lido por IOptions com
/// ValidateOnStart (arquitetura/10).
/// </summary>
internal sealed class BffOptions
{
    public const string SectionName = "Bff";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>Duração da sessão (B3/B7). A expiração autoritativa é a do AuthSession.</summary>
internal sealed class SessaoOptions
{
    public const string SectionName = "Sessao";

    [Range(typeof(TimeSpan), "00:01:00", "30.00:00:00")]
    public TimeSpan Duracao { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Quanto tempo uma sessão expirada ou revogada fica no banco antes do expurgo.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "3650.00:00:00")]
    public TimeSpan RetencaoAposEncerramento { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Intervalo entre execuções do expurgo (ExpurgoDeSessoesService).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan IntervaloDoExpurgo { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Bloqueio por tentativas e rate limiting do login local (B10).</summary>
internal sealed class LoginLocalOptions
{
    public const string SectionName = "LoginLocal";

    [Range(1, 100)]
    public int MaximoDeTentativas { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan DuracaoDoBloqueio { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Tentativas de POST /auth/login por IP, por minuto.</summary>
    [Range(1, 10_000)]
    public int TentativasPorMinutoPorIp { get; set; } = 10;
}

/// <summary>
/// Proxy reverso na frente da Api (decisão D4): redes das quais X-Forwarded-For/Proto são
/// aceitos. Sem isso, atrás do proxy toda requisição parece http (o cookie Secure e o
/// antiforgery falham) e o rate limiting por IP enxerga só o IP do proxy. Vazio = só loopback.
/// </summary>
internal sealed class ProxyReversoOptions
{
    public const string SectionName = "ProxyReverso";

    /// <summary>CIDR, ex.: "10.0.0.0/8".</summary>
    public string[] RedesConfiaveis { get; set; } = [];
}
