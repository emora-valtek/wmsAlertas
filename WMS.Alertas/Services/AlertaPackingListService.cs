using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

/// <summary>
/// Acceso Dapper a la cola persistente de alertas de Packing List. Todas las
/// actualizaciones exigen el GUID de reserva para evitar resultados cruzados
/// entre ejecuciones concurrentes.
/// </summary>
public class AlertaPackingListService
{
    private readonly IConfiguration _configuration;

    public AlertaPackingListService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Toma y reserva atómicamente un lote de alertas listas para procesar.
    /// </summary>
    public async Task<List<AlertaPackingListPendiente>> TomarPendientes(
        Guid procesadoPor,
        int cantidadMaxima = 50,
        int minutosAntiguedad = 0)
    {
        using var connection = CrearConexion();

        var alertas = await connection.QueryAsync<AlertaPackingListPendiente>(
            "dbo.spAlertaPackingListPendientesTomar",
            new
            {
                ProcesadoPor = procesadoPor,
                CantidadMaxima = cantidadMaxima,
                MinutosAntiguedad = minutosAntiguedad
            },
            commandType: CommandType.StoredProcedure);

        return alertas.ToList();
    }

    /// <summary>
    /// Marca una alerta como enviada si pertenece a la ejecución indicada.
    /// </summary>
    public async Task MarcarEnviada(
        long alertaPackingListId,
        Guid procesadoPor)
    {
        using var connection = CrearConexion();

        await connection.ExecuteAsync(
            "dbo.spAlertaPackingListMarcarEnviada",
            new
            {
                AlertaPackingListId = alertaPackingListId,
                ProcesadoPor = procesadoPor
            },
            commandType: CommandType.StoredProcedure);
    }

    /// <summary>
    /// Registra el fallo individual y calcula en BD el próximo intento.
    /// </summary>
    public async Task<bool> MarcarError(
        long alertaPackingListId,
        Guid procesadoPor,
        string mensajeError,
        int maximoIntentos = 5,
        int reintentarEnMinutos = 5)
    {
        using var connection = CrearConexion();

        var estado = await connection.QuerySingleAsync<string>(
            "dbo.spAlertaPackingListMarcarError",
            new
            {
                AlertaPackingListId = alertaPackingListId,
                ProcesadoPor = procesadoPor,
                MensajeError = mensajeError,
                MaximoIntentos = maximoIntentos,
                ReintentarEnMinutos = reintentarEnMinutos
            },
            commandType: CommandType.StoredProcedure);

        return estado == "ERROR";
    }

    /// <summary>
    /// Devuelve a la cola reservas abandonadas por procesos interrumpidos.
    /// </summary>
    public async Task<int> LiberarProcesamientosExpirados(
        int minutosExpiracion = 60)
    {
        using var connection = CrearConexion();

        return await connection.QuerySingleAsync<int>(
            "dbo.spAlertaPackingListProcesamientoExpiradoLiberar",
            new { MinutosExpiracion = minutosExpiracion },
            commandType: CommandType.StoredProcedure);
    }

    private SqlConnection CrearConexion()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }
}
