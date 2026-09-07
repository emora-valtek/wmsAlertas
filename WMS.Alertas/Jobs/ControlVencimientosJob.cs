using Hangfire;
using System.Diagnostics;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

/// <summary>
/// Envía a revisión las existencias que alcanzaron el umbral de vencimiento.
/// </summary>
public sealed class ControlVencimientosJob
{
    private readonly ControlVencimientosService _service;
    private readonly IAlertaEjecucionLogService _logService;
    private readonly CorreoService _correoService;
    private readonly IConfiguration _configuration;
    private readonly ExcelService _excelService;

    public ControlVencimientosJob(
        ControlVencimientosService service,
        IAlertaEjecucionLogService logService,
        CorreoService correoService,
        IConfiguration configuration,
        ExcelService excelService)
    {
        _service = service;
        _logService = logService;
        _correoService = correoService;
        _configuration = configuration;
        _excelService = excelService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 10800)]
    [AutomaticRetry(Attempts = 0)]
    public async Task EjecutarEnvioRevision()
    {
        const string tipoLog = "CONTROL_VENCIMIENTOS_REVISION";
        var logId = 0;
        var procesadas = 0;
        var lotes = 0;
        var pendientes = 0;
        var reloj = Stopwatch.StartNew();
        var tamano = Math.Clamp(_configuration.GetValue<int>("ControlVencimientos:TamanoLote"), 1, 1000);
        var pausa = Math.Clamp(_configuration.GetValue<int>("ControlVencimientos:PausaEntreLotesSegundos"), 0, 30);
        var maximo = Math.Clamp(_configuration.GetValue<int>("ControlVencimientos:MaximoSeguridadPorEjecucion"), 1, 100000);
        var duracion = TimeSpan.FromMinutes(Math.Clamp(
            _configuration.GetValue<int>("ControlVencimientos:DuracionMaximaMinutos"), 1, 720));

        try
        {
            logId = await _logService.Iniciar(tipoLog);
            var candidatos = (await _service.ObtenerDiagnostico())
                .ExistenciasVencidas
                .Take(maximo)
                .ToList();

            while (procesadas < maximo && reloj.Elapsed < duracion)
            {
                var resultado = await _service.EnviarLoteARevision(
                    Math.Min(tamano, maximo - procesadas));
                pendientes = resultado.Pendientes;
                if (resultado.Procesadas == 0)
                    break;

                procesadas += resultado.Procesadas;
                lotes++;
                if (pendientes > 0 && pausa > 0)
                    await Task.Delay(TimeSpan.FromSeconds(pausa));
            }

            var limitado = pendientes > 0 &&
                (procesadas >= maximo || reloj.Elapsed >= duracion);
            var resumen = $"Procesadas: {procesadas}; lotes: {lotes}; pendientes: {pendientes}; " +
                          $"duración: {reloj.Elapsed:hh\\:mm\\:ss}; límite: {(limitado ? "sí" : "no")}.";

            await _logService.FinalizarOk(logId, procesadas, resumen);
            await _correoService.EnviarCorreo(
                ["emora@valtek.cl"],
                "WMS: Existencias en revisión",
                $"""
                <h2>Existencias enviadas a revisión por vencimiento</h2>
                <p>Durante el proceso automático de control de vencimientos ejecutado durante la noche, se enviaron <strong>{procesadas} existencias</strong> a estado <strong>“En revisión”</strong>.</p>
                <p>Puedes revisar el detalle en el archivo Excel adjunto.</p>
                <p>Saludos,</p>
                """,
                _excelService.GenerarExcelExistenciasRevision(
                    candidatos.Take(procesadas)),
                $"ExistenciasRevision_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        catch (Exception ex)
        {
            if (logId == 0)
                logId = await _logService.Iniciar(tipoLog);

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }
}
