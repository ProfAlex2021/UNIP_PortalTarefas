namespace PortalTarefas.Tests.Apoio;

/// <summary>Relógio com data fixa para testes determinísticos de prazo.</summary>
public sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public static readonly RelogioFixo Padrao = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    public DateOnly Hoje => DateOnly.FromDateTime(agora.UtcDateTime);

    public override DateTimeOffset GetUtcNow() => agora;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
