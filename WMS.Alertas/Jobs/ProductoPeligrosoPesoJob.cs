using System.Net;
using System.Text;
using Hangfire;
using WMS.Alertas.Global;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public sealed class ProductoPeligrosoPesoJob
{
    private const string TipoAlerta = "ProductosPeligrososPeso";
    private const string TipoLog = "PRODUCTOS_PELIGROSOS_PESO";
    private const decimal PesoMaximoInflamablesKg = 3000m;
    private const decimal PesoMaximoPeligrososKg = 12000m;

    private readonly AlertaProductoPeligrosoPesoService _alertaService;
    private readonly CorreoService _correoService;
    private readonly CorreoDestinoService _correoDestinoService;
    private readonly IAlertaEjecucionLogService _logService;
    private readonly ConfiguracionEjecucionAlertas _configuracionEjecucion;
    private readonly ExcelService _excelService;

    public ProductoPeligrosoPesoJob(
        AlertaProductoPeligrosoPesoService alertaService,
        CorreoService correoService,
        CorreoDestinoService correoDestinoService,
        IAlertaEjecucionLogService logService,
        ConfiguracionEjecucionAlertas configuracionEjecucion,
        ExcelService excelService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _correoDestinoService = correoDestinoService;
        _logService = logService;
        _configuracionEjecucion = configuracionEjecucion;
        _excelService = excelService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Ejecutar()
    {
        var logId = 0;

        try
        {
            logId = await _logService.Iniciar(TipoLog);
            var anomalias = await _alertaService.ObtenerAnomalias(
                PesoMaximoInflamablesKg,
                PesoMaximoPeligrososKg);
            var sinPeso = anomalias
                .Where(x => x.TipoAnomalia == "SIN_PESO")
                .ToList();
            var sobrePeso = anomalias
                .Where(x => x.TipoAnomalia == "SOBRE_PESO")
                .ToList();

            if (_configuracionEjecucion.EsProduccion && anomalias.Count == 0)
            {
                await _logService.FinalizarOk(logId, 0, "Sin anomalías de peso");
                return;
            }

            var destinatarios = await _correoDestinoService.ObtenerCorreos(TipoAlerta);
            if (destinatarios.Count == 0 &&
                !_configuracionEjecucion.EsProduccion &&
                !string.IsNullOrWhiteSpace(_configuracionEjecucion.CorreoPruebas))
            {
                destinatarios.Add(_configuracionEjecucion.CorreoPruebas);
            }

            if (destinatarios.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No hay destinatarios configurados para {TipoAlerta}.");
            }

            var detallePesoAcumulado = await _alertaService.ObtenerDetallePesoAcumulado();
            var excelBytes = _excelService.GenerarExcelDetallePesoProductosPeligrosos(
                detallePesoAcumulado);

            await _correoService.EnviarCorreo(
                destinatarios,
                "Alerta WMS - Productos peligrosos",
                ArmarHtml(sinPeso, sobrePeso),
                excelBytes,
                "DetallePesoProductosPeligrosos.xlsx");

            await _logService.FinalizarOk(
                logId,
                anomalias.Count,
                string.Join(";", destinatarios));
        }
        catch (Exception ex)
        {
            if (logId == 0)
            {
                logId = await _logService.Iniciar(TipoLog);
            }

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private static string ArmarHtml(
        List<ProductoPeligrosoPesoAnomalo> sinPeso,
        List<ProductoPeligrosoPesoAnomalo> sobrePeso)
    {
        var html = new StringBuilder();
        html.AppendLine("<div style='font-family:Arial,Helvetica,sans-serif;color:#202124;max-width:900px;margin:0 auto;line-height:1.45;'>");
        AgregarSeccionProductos(html, sinPeso);
        AgregarSeccionPesoAcumulado(html, sobrePeso);
        html.AppendLine("<p style='margin:28px 0 0 0;'>Revise el detalle en el archivo adjunto.</p>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static void AgregarSeccionProductos(
        StringBuilder html,
        List<ProductoPeligrosoPesoAnomalo> productos)
    {
        html.AppendLine("<h2 style='font-size:21px;margin:28px 0 8px 0;'>Productos peligrosos sin peso definido</h2>");
        if (productos.Count == 0)
        {
            html.AppendLine("<p><em>No se encontraron productos.</em></p>");
            return;
        }

        html.AppendLine("<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:15px;'>");
        html.AppendLine("<tr><th align='left' width='25%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Código</th><th align='left' width='55%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Producto</th><th align='right' width='20%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Peso (kg)</th></tr>");

        foreach (var producto in productos)
        {
            var peso = producto.Peso?.ToString("N2") ?? "Sin informar";
            html.AppendLine("<tr>");
            AgregarCelda(html, producto.Codigo);
            AgregarCelda(html, producto.Nombre);
            AgregarCelda(html, peso, "right");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
    }

    private static void AgregarSeccionPesoAcumulado(
        StringBuilder html,
        List<ProductoPeligrosoPesoAnomalo> registros)
    {
        html.AppendLine("<h2 style='font-size:21px;margin:28px 0 8px 0;'>Peso acumulado sobre el límite permitido</h2>");
        if (registros.Count == 0)
        {
            html.AppendLine("<p><em>No se encontraron registros.</em></p>");
            return;
        }

        html.AppendLine("<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:15px;'>");
        html.AppendLine("<tr><th align='left' width='40%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Atributo</th><th align='right' width='30%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Peso acumulado (kg)</th><th align='right' width='30%' style='padding:10px 8px;border-bottom:2px solid #dadce0;'>Máximo permitido (kg)</th></tr>");

        foreach (var registro in registros)
        {
            var peso = registro.Peso?.ToString("N2") ?? "Sin informar";
            var pesoMaximo = registro.EsInflamable == true
                ? PesoMaximoInflamablesKg.ToString("N2")
                : PesoMaximoPeligrososKg.ToString("N2");
            html.AppendLine("<tr>");
            AgregarCelda(html, registro.Tipo);
            AgregarCelda(html, peso, "right");
            AgregarCelda(html, pesoMaximo, "right");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
    }

    private static void AgregarCelda(
        StringBuilder html,
        string texto,
        string alineacion = "left")
    {
        html.AppendLine($"<td align='{alineacion}' valign='top' style='padding:13px 8px;border-bottom:1px solid #eeeeee;'>{WebUtility.HtmlEncode(texto)}</td>");
    }
}
