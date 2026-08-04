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

        public async Task<PendientesGestionSacResultado> ObtenerPendientesGestionSac()
        {
            using var connection = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using var resultados = await connection.QueryMultipleAsync(
                "spAlerta_StockAsignadoSinPL",
                commandType: CommandType.StoredProcedure);

            return new PendientesGestionSacResultado
            {
                StockSinPackingList = (await resultados.ReadAsync<StockAsignadoSinPL>()).ToList(),
                PackingListsDevueltos = (await resultados.ReadAsync<PackingListDevueltoSac>()).ToList()
            };
        }
    }
}
