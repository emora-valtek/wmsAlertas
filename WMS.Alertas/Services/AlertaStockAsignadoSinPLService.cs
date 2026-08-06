using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services
{
    /// <summary>
    /// Consulta exclusivamente el resultado diario de stock asignado sin PL.
    /// Las devoluciones a SAC se leen desde la cola AlertaPackingList.
    /// </summary>
    public class AlertaStockAsignadoSinPLService
    {
        private readonly IConfiguration _configuration;

        public AlertaStockAsignadoSinPLService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Ejecuta el SP de un solo conjunto de resultados y lo mapea con Dapper.
        /// </summary>
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
