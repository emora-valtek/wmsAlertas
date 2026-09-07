using Hangfire;
using System.Diagnostics;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

/// <summary>
/// Levanta una fotografía de los registros que serían procesados por el futuro
/// control nocturno de vencimientos. No realiza actualizaciones ni envía correo.
/// </summary>
public sealed class ControlVencimientosJob
{
    private const string TipoLog = "DIAGNOSTICO_CONTROL_VENCIMIENTOS";

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

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task EjecutarDiagnostico()
    {
        var logId = 0;

        try
        {
            logId = await _logService.Iniciar(TipoLog);
            var diagnostico = await _service.ObtenerDiagnostico();

            var proximas = diagnostico.SolicitudesLote.Count(
                x => x.Situacion == "PROXIMO_A_VENCER");
            var vencidas = diagnostico.SolicitudesLote.Count(
                x => x.Situacion == "VENCIDO");
            var total = diagnostico.ExistenciasVencidas.Count
                + diagnostico.SolicitudesVigenciaVencida.Count
                + diagnostico.SolicitudesLote.Count;

            var resumen =
                $"Solo diagnóstico; no se modificaron datos. " +
                $"Existencias para revisión: {diagnostico.ExistenciasVencidas.Count}; " +
                $"reservas con vigencia vencida: {diagnostico.SolicitudesVigenciaVencida.Count}; " +
                $"reservas con lote próximo a vencer: {proximas}; " +
                $"reservas con lote vencido: {vencidas}.";

            await _logService.FinalizarOk(logId, total, resumen);
        }
        catch (Exception ex)
        {
            if (logId == 0)
                logId = await _logService.Iniciar(TipoLog);

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 10800)]
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
                var resultado = await _service.EnviarLoteARevision(Math.Min(tamano, maximo - procesadas));
                pendientes = resultado.Pendientes;
                if (resultado.Procesadas == 0) break;
                procesadas += resultado.Procesadas;
                lotes++;
                if (pendientes > 0 && pausa > 0)
                    await Task.Delay(TimeSpan.FromSeconds(pausa));
            }

            var limitado = pendientes > 0 && (procesadas >= maximo || reloj.Elapsed >= duracion);
            var resumen = $"Procesadas: {procesadas}; lotes: {lotes}; pendientes: {pendientes}; " +
                          $"duración: {reloj.Elapsed:hh\\:mm\\:ss}; límite: {(limitado ? "sí" : "no")}.";
            await _logService.FinalizarOk(logId, procesadas, resumen);
            await _correoService.EnviarCorreo(
                ["emora@valtek.cl"],
                "Control de vencimientos WMS - envío a revisión",
                $"<h2>Control de vencimientos WMS</h2><p>{resumen}</p>",
                _excelService.GenerarExcelExistenciasRevision(
                    candidatos.Take(procesadas)),
                $"ExistenciasRevision_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        catch (Exception ex)
        {
            if (logId == 0) logId = await _logService.Iniciar(tipoLog);
            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task EjecutarLotesReservados()
    {
        const string tipoLog = "CONTROL_VENCIMIENTOS_RESERVAS";
        var logId = 0;
        try
        {
            logId = await _logService.Iniciar(tipoLog);
            var diagnostico = await _service.ObtenerDiagnostico();
            var vigencia = diagnostico.SolicitudesVigenciaVencida;
            var loteVencido = diagnostico.SolicitudesLote
                .Where(x => x.Situacion == "VENCIDO").ToList();

            foreach (var item in vigencia)
                await _service.CaducarSolicitud(item.SolicitudLoteReservadoId, "VIGENCIA_SOLICITUD");
            foreach (var item in loteVencido)
                await _service.CaducarSolicitud(item.SolicitudLoteReservadoId, "VENCIMIENTO_LOTE");

            var total = vigencia.Count + loteVencido.Count;
            var filas = string.Join("", vigencia.Select(x =>
                    $"<tr><td>Vigencia</td><td>{x.ProductoId}</td><td>{x.LoteId}</td><td>{x.CantidadProductos}</td><td>{x.ClienteNombre}</td></tr>")
                .Concat(loteVencido.Select(x =>
                    $"<tr><td>Lote vencido</td><td>{x.ProductoCodigo}</td><td>{x.LoteCodigo}</td><td>{x.CantidadProductos}</td><td>{x.ClienteNombre}</td></tr>")));
            var cuerpo = $"<h2>Lotes reservados procesados</h2><p>Por vigencia: {vigencia.Count}. Por lote vencido: {loteVencido.Count}.</p>" +
                "<table border='1' cellpadding='5'><tr><th>Motivo</th><th>Producto</th><th>Lote</th><th>Cantidad</th><th>Cliente</th></tr>" + filas + "</table>";
            await _logService.FinalizarOk(logId, total, "emora@valtek.cl");
            await _correoService.EnviarCorreo(["emora@valtek.cl"],
                "Control de vencimientos WMS - lotes reservados", cuerpo);
        }
        catch (Exception ex)
        {
            if (logId == 0) logId = await _logService.Iniciar(tipoLog);
            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }
}
