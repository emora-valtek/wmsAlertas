using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public sealed class ActualizacionProductosManagerService
{
    private readonly IConfiguration _configuration;
    private readonly ManagerProductosClient _managerClient;

    public ActualizacionProductosManagerService(
        IConfiguration configuration,
        ManagerProductosClient managerClient)
    {
        _configuration = configuration;
        _managerClient = managerClient;
    }

    public async Task<ResultadoActualizacionProductosManager> Actualizar(CancellationToken cancellationToken)
    {
        var productosManager = await _managerClient.ObtenerProductos(cancellationToken);
        await using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync(cancellationToken);

        var usuarioJob = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TRY_CAST(CGS_Valor AS INT) FROM dbo.CGS_ConfiguracionSistema WHERE CGS_Codigo = 'JOB_USUID';",
                cancellationToken: cancellationToken));
        if (usuarioJob is not > 0)
            throw new InvalidOperationException("JOB_USUID no está configurado correctamente para el usuario técnico de los jobs.");

        var productosWms = (await connection.QueryAsync<(string Codigo, string Nombre)>(
            new CommandDefinition(
                "SELECT PRO_Codigo AS Codigo, PRO_Nombre AS Nombre FROM dbo.PRO_Producto;",
                cancellationToken: cancellationToken)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var creados = 0;
        var actualizados = 0;
        var sinCambios = 0;
        var errores = 0;
        var detalleErrores = new List<string>();

        foreach (var productoManager in productosManager
                     .GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.First()))
        {
            try
            {
                var codigo = productoManager.Codigo.Trim();
                var nombre = string.IsNullOrWhiteSpace(productoManager.Nombre)
                    ? codigo
                    : productoManager.Nombre.Trim();

                if (!productosWms.TryGetValue(codigo, out var productoWms))
                {
                    var creado = await connection.QuerySingleAsync<(int ProductoId, bool FueCreado)>(
                        new CommandDefinition(
                            "dbo.spProductoManagerCrear",
                            new { Codigo = codigo, Nombre = nombre, Descripcion = nombre, CreadoPor = usuarioJob.Value },
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: cancellationToken));

                    if (creado.FueCreado)
                        creados++;
                    else
                        sinCambios++;

                    productosWms[codigo] = (codigo, nombre);
                    continue;
                }

                if (string.Equals(productoWms.Nombre?.Trim(), nombre, StringComparison.Ordinal))
                {
                    sinCambios++;
                    continue;
                }

                var nombreActualizado = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "dbo.spProductoManagerNombreActualizar",
                        new { Codigo = codigo, Nombre = nombre, ModificadoPor = usuarioJob.Value },
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: cancellationToken));
                if (nombreActualizado)
                    actualizados++;
                else
                    sinCambios++;
            }
            catch (Exception ex)
            {
                errores++;
                if (detalleErrores.Count < 20)
                {
                    detalleErrores.Add(
                        $"{productoManager.Codigo.Trim()}: {ex.Message}");
                }
            }
        }

        return new ResultadoActualizacionProductosManager(
            productosManager.Count,
            creados,
            actualizados,
            sinCambios,
            errores,
            string.Join(" | ", detalleErrores));
    }
}
