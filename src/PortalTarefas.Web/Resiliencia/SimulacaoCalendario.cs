namespace PortalTarefas.Web.Resiliencia;

public enum ModoCalendario
{
    Normal,
    /// <summary>As próximas N chamadas falham com 503 e depois a dependência volta.</summary>
    FalhaTransitoria,
    /// <summary>Todas as chamadas falham com 503.</summary>
    Indisponivel,
    /// <summary>Responde depois de 3 s (acima do timeout por tentativa).</summary>
    Lento
}

/// <summary>Estado da dependência simulada (singleton, somente em Development).</summary>
public sealed class EstadoSimulacaoCalendario
{
    private int _falhasRestantes;
    public ModoCalendario Modo { get; private set; } = ModoCalendario.Normal;
    public int ChamadasRecebidas;

    public void Definir(ModoCalendario modo, int falhas = 2)
    {
        Modo = modo;
        _falhasRestantes = falhas;
        ChamadasRecebidas = 0;
    }

    /// <summary>Decide se a chamada atual deve falhar.</summary>
    public bool DeveFalhar()
    {
        Interlocked.Increment(ref ChamadasRecebidas);
        return Modo switch
        {
            ModoCalendario.Indisponivel => true,
            ModoCalendario.FalhaTransitoria => Interlocked.Decrement(ref _falhasRestantes) >= 0,
            _ => false
        };
    }
}

/// <summary>
/// Dependência externa SIMULADA: "serviço de calendário" com endpoints para mudar o
/// comportamento. Mapeada somente quando Simulacao:Habilitada = true (Development).
/// </summary>
public static class SimulacaoCalendarioEndpoints
{
    public static void MapSimulacaoCalendario(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/simulacao/calendario").ExcludeFromDescription().AllowAnonymous();

        grupo.MapGet("/feriados/{ano:int}", async (int ano, EstadoSimulacaoCalendario estado, ILogger<EstadoSimulacaoCalendario> logger) =>
        {
            if (estado.Modo == ModoCalendario.Lento)
                await Task.Delay(TimeSpan.FromSeconds(3));

            if (estado.DeveFalhar())
            {
                logger.LogWarning("Simulação do calendário respondeu 503 (modo {Modo})", estado.Modo);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new Feriado[]
            {
                new(new DateOnly(ano, 1, 1), "Confraternização Universal"),
                new(new DateOnly(ano, 4, 21), "Tiradentes"),
                new(new DateOnly(ano, 5, 1), "Dia do Trabalho"),
                new(new DateOnly(ano, 9, 7), "Independência do Brasil"),
                new(new DateOnly(ano, 10, 12), "Nossa Senhora Aparecida"),
                new(new DateOnly(ano, 11, 2), "Finados"),
                new(new DateOnly(ano, 11, 15), "Proclamação da República"),
                new(new DateOnly(ano, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra"),
                new(new DateOnly(ano, 12, 25), "Natal"),
            });
        });

        grupo.MapGet("/saude", (EstadoSimulacaoCalendario estado) =>
            estado.Modo == ModoCalendario.Indisponivel ? Results.StatusCode(503) : Results.Ok("ok"));

        // Ex.: POST /simulacao/calendario/modo?modo=FalhaTransitoria&falhas=2
        grupo.MapPost("/modo", (ModoCalendario modo, int? falhas, EstadoSimulacaoCalendario estado) =>
        {
            estado.Definir(modo, falhas ?? 2);
            return Results.Ok(new { modo = estado.Modo.ToString() });
        });
    }
}
