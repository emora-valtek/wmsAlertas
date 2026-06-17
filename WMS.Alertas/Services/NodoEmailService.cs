using Microsoft.Extensions.Configuration;
using System.Data;
using WMS.Alertas.Global;
using WMS.Alertas.Models;
using Microsoft.Data.SqlClient;
using Dapper;


namespace WMS.Alertas.Services
{
    public class NodoEmailService
    {
        private readonly IConfiguration _configuration;

        public NodoEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<NodoEmailDto?> ObtenerConfiguracion()
        {
            using var connection = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            var nodoEmail =
                await connection.QueryFirstOrDefaultAsync<NodoEmailDto>(
                    "dbo.spConfiguracionEmailObtener",
                    commandType: CommandType.StoredProcedure);

            if (nodoEmail == null)
                return null;

            nodoEmail.EmailContrasena =
                Encryption.Decrypt(nodoEmail.EmailContrasena);

            return nodoEmail;
        }
    }
}
