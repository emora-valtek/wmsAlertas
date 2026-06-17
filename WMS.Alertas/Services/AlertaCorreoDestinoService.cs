using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace WMS.Alertas.Services;

public class AlertaCorreoDestinoService
{
    private readonly IConfiguration _configuration;

    public AlertaCorreoDestinoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<string>> ObtenerCorreos(string tipoAlerta)
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var correos = await connection.QueryAsync<string>(
            "spAlertasCorreoDestinoObtener",
            new { TipoAlerta = tipoAlerta },
            commandType: CommandType.StoredProcedure);

        return correos.ToList();
    }
}