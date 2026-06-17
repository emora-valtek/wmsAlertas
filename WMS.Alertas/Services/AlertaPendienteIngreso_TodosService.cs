using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaPendienteIngreso_TodosService
{
    private readonly IConfiguration _configuration;

    public AlertaPendienteIngreso_TodosService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    //obtiene la lista de productos pendientes de ingreso de Mercadería
    public async Task<List<PendienteIngresoDto>> ObtenerPendientesIngresoTodos()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDto>(
            "spAlerta_PendIngresoTodosObtener",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }

    //obtiene el detalle de las existencias pendientes de ingreso de Mercadería
    public async Task<List<PendienteIngresoDetalleDto>> ObtenerPendientesIngresoDetalleTodos()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDetalleDto>(
            "spAlerta_PendIngresoTodosDetalle",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }
}