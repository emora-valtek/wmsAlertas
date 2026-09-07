using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

/// <summary>
/// Obtiene los candidatos de los procesos de vencimiento. Esta primera versión
/// es exclusivamente diagnóstica y no modifica inventario ni solicitudes.
/// </summary>
public sealed class ControlVencimientosService
{
    private readonly IConfiguration _configuration;

    public ControlVencimientosService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ControlVencimientosDiagnostico> ObtenerLotesReservados()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        using var resultados = await connection.QueryMultipleAsync(
            "dbo.spLoteReservadosVencimientoObtener",
            commandType: CommandType.StoredProcedure);

        return new ControlVencimientosDiagnostico
        {
            SolicitudesVigenciaVencida =
                (await resultados.ReadAsync<SolicitudVigenciaVencidaDiagnostico>()).ToList(),
            SolicitudesLote =
                (await resultados.ReadAsync<SolicitudLotePorVencerDiagnostico>()).ToList()
        };
    }

    public async Task<ResultadoLoteVencimientos> EnviarLoteARevision(
        int cantidadMaxima,
        int ejecucionLogId)
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        return await connection.QuerySingleAsync<ResultadoLoteVencimientos>(
            "dbo.spVencimientosRevisionar",
            new
            {
                CantidadMaxima = cantidadMaxima,
                EjecucionLogId = ejecucionLogId
            },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 300);
    }

    public async Task<List<ExistenciaVencidaDiagnostico>> ObtenerInformeRevisionPendiente()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var registros = await connection.QueryAsync<ExistenciaVencidaDiagnostico>(
            "dbo.spVencimientosInformeObtener",
            commandType: CommandType.StoredProcedure);
        return registros.ToList();
    }

    public async Task MarcarInformeRevisionEnviado(IEnumerable<int> informeIds)
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var informeId in informeIds.Distinct())
            {
                await connection.ExecuteAsync(
                    "dbo.spVencimientosInformeMarcar",
                    new { InformeId = informeId },
                    transaction,
                    commandType: CommandType.StoredProcedure);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task CaducarSolicitud(int solicitudId, string motivo)
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
        var usuarioId = await connection.ExecuteScalarAsync<int?>(
            "SELECT TRY_CAST(CGS_Valor AS INT) FROM dbo.CGS_ConfiguracionSistema WHERE CGS_Codigo='JOB_USUID'");
        if (usuarioId is null)
            throw new InvalidOperationException("JOB_USUID no está configurado correctamente.");

        await connection.ExecuteAsync(
            "dbo.spSolicitudLoteReservadoCaducar",
            new { Id = solicitudId, CreadoPor = usuarioId.Value, MotivoLiberacion = motivo },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 120);
    }
}
