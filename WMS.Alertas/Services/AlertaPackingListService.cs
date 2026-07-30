using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaPackingListService
{
    private readonly IConfiguration _configuration;

    public AlertaPackingListService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<AlertaPackingListPendiente>> TomarPendientes(
        Guid procesadoPor,
        int cantidadMaxima = 50)
    {
        using var connection = CrearConexion();

        var alertas = await connection.QueryAsync<AlertaPackingListPendiente>(
            "dbo.spAlertaPackingListPendientesTomar",
            new
            {
                ProcesadoPor = procesadoPor,
                CantidadMaxima = cantidadMaxima
            },
            commandType: CommandType.StoredProcedure);

        return alertas.ToList();
    }

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

    public async Task MarcarError(
        long alertaPackingListId,
        Guid procesadoPor,
        string mensajeError,
        int maximoIntentos = 5,
        int reintentarEnMinutos = 5)
    {
        using var connection = CrearConexion();

        await connection.ExecuteAsync(
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
    }

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
