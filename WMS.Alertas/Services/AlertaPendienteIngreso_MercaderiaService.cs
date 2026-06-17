using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class AlertaPendienteIngreso_MercaderiaService
{
    private readonly IConfiguration _configuration;

    public AlertaPendienteIngreso_MercaderiaService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    //obtiene la lista de productos pendientes de ingreso de Mercadería
    public async Task<List<PendienteIngresoDto>> ObtenerPendientesIngresoMercaderia()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDto>(
            "spAlerta_PendIngresoMercaderiaObtener",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }

    //obtiene el detalle de las existencias pendientes de ingreso de Mercadería
    public async Task<List<PendienteIngresoDetalleDto>> ObtenerPendientesIngresoDetalleMercaderia()
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var resultado = await connection.QueryAsync<PendienteIngresoDetalleDto>(
            "spAlerta_PendIngresoMercaderiaDetalle",
            commandType: CommandType.StoredProcedure);

        return resultado.ToList();
    }
}