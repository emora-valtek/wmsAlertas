using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services
{
    public class AlertaStockAsignadoSinPLService
    {
        private readonly IConfiguration _configuration;

        public AlertaStockAsignadoSinPLService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<StockAsignadoSinPL>> ObtenerStockAsignadoSinPL()
        {
            using var connection = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            var resultado = await connection.QueryAsync<StockAsignadoSinPL>(
                "spAlerta_StockAsignadoSinPL",
                commandType: CommandType.StoredProcedure);

            return resultado.ToList();
        }
    }
}
