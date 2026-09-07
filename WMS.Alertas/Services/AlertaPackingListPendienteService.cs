using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public sealed class AlertaPackingListPendienteService
{
    private readonly IConfiguration _configuration;

    public AlertaPackingListPendienteService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<PackingListPendiente>> ObtenerPendientes()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var registros = await connection.QueryAsync<PackingListPendiente>(
            "dbo.spPackingListPendienteObtener",
            commandType: CommandType.StoredProcedure);

        return registros.ToList();
    }
}
