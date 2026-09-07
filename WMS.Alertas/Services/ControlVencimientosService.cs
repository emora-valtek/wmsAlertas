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

    public async Task<ControlVencimientosDiagnostico> ObtenerDiagnostico()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        using var resultados = await connection.QueryMultipleAsync(
            "dbo.spVencimientosObtener",
            commandType: CommandType.StoredProcedure);

        return new ControlVencimientosDiagnostico
        {
            ExistenciasVencidas =
                (await resultados.ReadAsync<ExistenciaVencidaDiagnostico>()).ToList(),
            SolicitudesVigenciaVencida =
                (await resultados.ReadAsync<SolicitudVigenciaVencidaDiagnostico>()).ToList(),
            SolicitudesLote =
                (await resultados.ReadAsync<SolicitudLotePorVencerDiagnostico>()).ToList()
        };
    }

    public async Task<ResultadoLoteVencimientos> EnviarLoteARevision(int cantidadMaxima)
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        return await connection.QuerySingleAsync<ResultadoLoteVencimientos>(
            "dbo.spVencimientosRevisionar",
            new { CantidadMaxima = cantidadMaxima },
            commandType: CommandType.StoredProcedure,
            commandTimeout: 300);
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
