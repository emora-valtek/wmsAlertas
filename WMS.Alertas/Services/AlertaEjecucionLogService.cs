using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using WMS.Alertas.Interfaces;

namespace WMS.Alertas.Services;

public class AlertaEjecucionLogService : IAlertaEjecucionLogService
{
    private readonly IConfiguration _configuration;

    public AlertaEjecucionLogService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<int> Iniciar(string tipoAlerta)
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var sql = @"
            INSERT INTO dbo.AlertasEjecucionLog
            (
                 TipoAlerta
                ,FechaInicio
                ,Estado
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                 @TipoAlerta
                ,GETDATE()
                ,'EN_PROCESO'
            );";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            TipoAlerta = tipoAlerta
        });
    }

    public async Task FinalizarOk(int id, int cantidadRegistros, string destinatarios)
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var sql = @"
            UPDATE dbo.AlertasEjecucionLog
            SET
                 FechaFin = GETDATE()
                ,Estado = 'OK'
                ,CantidadRegistros = @CantidadRegistros
                ,Destinatarios = @Destinatarios
                ,MensajeError = NULL
            WHERE Id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            CantidadRegistros = cantidadRegistros,
            Destinatarios = destinatarios
        });
    }

    public async Task FinalizarError(int id, string mensajeError)
    {
        using var connection = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        var sql = @"
            UPDATE dbo.AlertasEjecucionLog
            SET
                 FechaFin = GETDATE()
                ,Estado = 'ERROR'
                ,MensajeError = @MensajeError
            WHERE Id = @Id;";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            MensajeError = mensajeError
        });
    }
}