namespace SharedKernel.Messaging;

/// <summary>Configuração da idempotência (seção "Idempotencia" do appsettings).</summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotencia";

    /// <summary>
    /// Depois deste tempo uma reserva "em andamento" é considerada abandonada (processo caiu
    /// entre a reserva e o commit) e pode ser assumida por um reenvio.
    /// </summary>
    public TimeSpan ReservationTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
