using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public sealed class AlertaProductoPeligrosoPesoService
{
    private readonly IConfiguration _configuration;

    public AlertaProductoPeligrosoPesoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<ProductoPeligrosoPesoAnomalo>> ObtenerAnomalias(
        decimal pesoMaximoInflamablesKg,
        decimal pesoMaximoPeligrososKg)
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var registros = await connection.QueryAsync<ProductoPeligrosoPesoAnomalo>(
            "dbo.spAlertaProductoPeligrosoPesoObtener",
            new
            {
                PesoMaximoInflamablesKg = pesoMaximoInflamablesKg,
                PesoMaximoPeligrososKg = pesoMaximoPeligrososKg
            },
            commandType: CommandType.StoredProcedure);

        return registros.ToList();
    }

    public async Task<List<ProductoPeligrosoPesoDetalle>> ObtenerDetallePesoAcumulado()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var registros = await connection.QueryAsync<ProductoPeligrosoPesoDetalle>(
            "dbo.spAlertaProductoPeligrosoPesoDetalleObtener",
            commandType: CommandType.StoredProcedure);

        return registros.ToList();
    }

}
