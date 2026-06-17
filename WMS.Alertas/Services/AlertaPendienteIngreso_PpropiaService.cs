using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaPendienteIngreso_PpropiaService
{
    private readonly IConfiguration _configuration;

    public AlertaPendienteIngreso_PpropiaService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    //obtiene la lista de productos pendientes de ingreso de Produccion propia
    public async Task<List<PendienteIngresoDto>> ObtenerPendientesIngreso_prod_propia()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDto>(
            "spAlerta_PendIngresoProdPropiaObtener",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }

    //obtiene el detalle de las existencias pendientes de ingreso de prod propia
    public async Task<List<PendienteIngresoDetalleDto>> ObtenerPendientesIngresoDetalle_prod_propia()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDetalleDto>(
            "spAlerta_PendIngresoProdPropiaDetalle",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }
}