using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaPendienteIngresoService
{
    private readonly IConfiguration _configuration;

    public AlertaPendienteIngresoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<PendienteIngresoDto>> ObtenerPendientesIngreso()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDto>(
            "spAlertaExistenciasPendientesIngreso",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }

    public async Task<List<PendienteIngresoDetalleDto>> ObtenerPendientesIngresoDetalle()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDetalleDto>(
            "spAlertaPendientesIngresoDetalle",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }
}