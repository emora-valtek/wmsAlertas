using Dapper;
using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WMS.Alertas.Global;

namespace WMS.Alertas.Health;

public sealed class WmsAlertasHealthCheck : IHealthCheck
{
    private static readonly string[] JobsDiariosEsperados =
    {
        "PENDIENTE_INGRESO_PRODUCCION_PROPIA",
        "PENDIENTE_INGRESO_MERCADERIA",
        "PENDIENTE_INGRESO_TODOS",
        "STOCK_ASIGNADO_SIN_PL",
        "LOTE_RESERVADO_MINIMO"
    };

    private readonly IConfiguration _configuration;
    private readonly ConfiguracionEjecucionAlertas _configuracionEjecucion;

    public WmsAlertasHealthCheck(
        IConfiguration configuration,
        ConfiguracionEjecucionAlertas configuracionEjecucion)
    {
        _configuration = configuration;
        _configuracionEjecucion = configuracionEjecucion;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return HealthCheckResult.Unhealthy(
                    "No existe una conexión configurada.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT 1;",
                    cancellationToken: cancellationToken));

            if (!_configuracionEjecucion.Habilitadas)
            {
                return HealthCheckResult.Healthy(
                    "Alertas deshabilitadas en este ambiente; la base de datos responde.");
            }

            var servidores = JobStorage.Current
                .GetMonitoringApi()
                .Servers();
            var limiteHeartbeat = DateTime.UtcNow.AddMinutes(-2);

            if (!servidores.Any(x =>
                    x.Heartbeat.HasValue &&
                    x.Heartbeat.Value.ToUniversalTime() >= limiteHeartbeat))
            {
                return HealthCheckResult.Unhealthy(
                    "Hangfire no registra un servidor activo.");
            }

            if (!_configuracionEjecucion.ProgramacionAutomatica)
            {
                return HealthCheckResult.Healthy(
                    "Modo manual activo; Hangfire y la base de datos responden.");
            }

            var ahoraChile = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                ConfiguracionAlertas.ZonaHorariaChile);

            // Antes de las 09:15 todavía no corresponde exigir las ejecuciones
            // diarias que finalizan su programación a las 09:05.
            if (ahoraChile.DayOfWeek is not DayOfWeek.Saturday and
                not DayOfWeek.Sunday &&
                ahoraChile.TimeOfDay >= new TimeSpan(9, 15, 0))
            {
                const string sql = """
                    ;WITH UltimasEjecuciones AS
                    (
                        SELECT
                             TipoAlerta
                            ,Estado
                            ,ROW_NUMBER() OVER
                            (
                                PARTITION BY TipoAlerta
                                ORDER BY FechaInicio DESC, Id DESC
                            ) AS Fila
                        FROM dbo.AlertasEjecucionLog
                        WHERE FechaInicio >= CONVERT(date, GETDATE())
                          AND FechaInicio < DATEADD(day, 1, CONVERT(date, GETDATE()))
                          AND TipoAlerta IN @Tipos
                    )
                    SELECT TipoAlerta, Estado
                    FROM UltimasEjecuciones
                    WHERE Fila = 1;
                    """;

                var ejecuciones = (await connection.QueryAsync<EjecucionDiaria>(
                        new CommandDefinition(
                            sql,
                            new { Tipos = JobsDiariosEsperados },
                            cancellationToken: cancellationToken)))
                    .ToDictionary(
                        x => x.TipoAlerta,
                        x => x.Estado,
                        StringComparer.OrdinalIgnoreCase);
                var faltantes = JobsDiariosEsperados
                    .Where(x => !ejecuciones.ContainsKey(x))
                    .ToArray();

                if (faltantes.Length > 0)
                {
                    return HealthCheckResult.Unhealthy(
                        $"No existe ejecución diaria de: {string.Join(", ", faltantes)}.");
                }

                var noFinalizadosOk = ejecuciones
                    .Where(x => !x.Value.Equals(
                        "OK",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"{x.Key} ({x.Value})")
                    .ToArray();

                if (noFinalizadosOk.Length > 0)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Jobs diarios sin término OK: {string.Join(", ", noFinalizadosOk)}.");
                }
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Falló la comprobación de WMS.Alertas.",
                ex);
        }
    }

    private sealed class EjecucionDiaria
    {
        public string TipoAlerta { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
    }
}
