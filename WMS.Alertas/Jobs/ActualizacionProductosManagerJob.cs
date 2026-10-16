using Hangfire;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public sealed class ActualizacionProductosManagerJob
{
    private const string TipoLog = "ACTUALIZACION_PRODUCTOS_MANAGER";
    private readonly ActualizacionProductosManagerService _service;
    private readonly IAlertaEjecucionLogService _logService;

    public ActualizacionProductosManagerJob(
        ActualizacionProductosManagerService service,
        IAlertaEjecucionLogService logService)
    {
        _service = service;
        _logService = logService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Ejecutar()
    {
        var logId = 0;
        try
        {
            logId = await _logService.Iniciar(TipoLog);
            var resultado = await _service.Actualizar(CancellationToken.None);
            await _logService.FinalizarOk(
                logId,
                resultado.Creados + resultado.NombresActualizados,
                $"Consultados: {resultado.Consultados}; creados: {resultado.Creados}; " +
                $"nombres actualizados: {resultado.NombresActualizados}; sin cambios: {resultado.SinCambios}; " +
                $"errores por producto: {resultado.Errores}. " +
                $"Detalle: {resultado.DetalleErrores}");
        }
        catch (Exception ex)
        {
            if (logId == 0)
                logId = await _logService.Iniciar(TipoLog);
            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }
}
