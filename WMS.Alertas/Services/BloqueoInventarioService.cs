using Dapper;
using Microsoft.Data.SqlClient;

namespace WMS.Alertas.Services;

/// <summary>
/// Determina si los procesos automáticos que modifican inventario deben
/// suspenderse por un inventario vigente.
/// </summary>
public sealed class BloqueoInventarioService
{
    private readonly IConfiguration _configuration;

    public BloqueoInventarioService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<BloqueoInventario?> ObtenerBloqueo()
    {
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        const string sql = """
            SELECT TOP (1)
                 Estado
                ,FechaInicioProgramada
            FROM dbo.INV_Inventario
            WHERE Activo = 1
              AND
              (
                  Estado = 2
                  OR
                  (
                      Estado = 5
                      AND FechaInicioProgramada <= CONVERT(date, GETDATE())
                  )
              )
            ORDER BY FechaInicioProgramada DESC;
            """;

        return await connection.QuerySingleOrDefaultAsync<BloqueoInventario>(sql);
    }
}

public sealed class BloqueoInventario
{
    public int Estado { get; init; }
    public DateTime? FechaInicioProgramada { get; init; }

    public string Descripcion => Estado == 2
        ? "inventario en curso"
        : $"inventario activado desde el {FechaInicioProgramada:dd-MM-yyyy}";
}
