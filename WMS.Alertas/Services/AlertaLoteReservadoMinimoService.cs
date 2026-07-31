using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaLoteReservadoMinimoService
{
    private readonly IConfiguration _configuration;

    public AlertaLoteReservadoMinimoService(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<LoteReservadoMinimo>> ObtenerPendientes()
    {
        using var connection = CrearConexion();

        var lotes = await connection.QueryAsync<LoteReservadoMinimo>(
            "dbo.spAlertaLoteReservadoMinimoObtener",
            commandType: CommandType.StoredProcedure);

        return lotes.ToList();
    }

    public async Task MarcarEnviadas(IEnumerable<int> solicitudesIds)
    {
        var ids = solicitudesIds.Distinct().ToList();

        if (ids.Count == 0)
            return;

        using var connection = CrearConexion();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var solicitudId in ids)
            {
                await connection.ExecuteAsync(
                    "dbo.spAlertaLoteReservadoMinimoMarcarEnviada",
                    new { SolicitudLoteReservadoId = solicitudId },
                    transaction,
                    commandType: CommandType.StoredProcedure);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private SqlConnection CrearConexion()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }
}
