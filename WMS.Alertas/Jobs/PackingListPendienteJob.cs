using System.Net;
using System.Text;
using Hangfire;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public sealed class PackingListPendienteJob
{
    private readonly AlertaPackingListPendienteService _alertaService;
    private readonly CorreoService _correoService;
    private readonly IAlertaEjecucionLogService _logService;

    public PackingListPendienteJob(
        AlertaPackingListPendienteService alertaService,
        CorreoService correoService,
        IAlertaEjecucionLogService logService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _logService = logService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Ejecutar()
    {
        const string tipoLog = "PACKING_LIST_PENDIENTE";
        var logId = 0;

        try
        {
            logId = await _logService.Iniciar(tipoLog);
            var registros = await _alertaService.ObtenerPendientes();
            var recoleccion = registros.Where(x => x.Etapa == "RECOLECCION").ToList();
            var embalaje = registros.Where(x => x.Etapa == "EMBALAJE").ToList();

            await _correoService.EnviarCorreo(
                ["emora@valtek.cl"],
                "WMS: Alerta Packing List pendientes",
                ArmarHtml(recoleccion, embalaje));

            await _logService.FinalizarOk(
                logId,
                registros.Count,
                "emora@valtek.cl");
        }
        catch (Exception ex)
        {
            if (logId == 0)
                logId = await _logService.Iniciar(tipoLog);

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private static string ArmarHtml(
        List<PackingListPendiente> recoleccion,
        List<PackingListPendiente> embalaje)
    {
        var html = new StringBuilder();
        html.AppendLine("<div style='font-family:Arial,Helvetica,sans-serif;color:#202124;max-width:900px;margin:0 auto;line-height:1.45;'>");
        html.AppendLine("<h1 style='font-size:28px;font-weight:600;margin:0 0 16px 0;'>Packing List pendientes</h1>");
        html.AppendLine("<p style='font-size:17px;margin:0 0 28px 0;'>Los siguientes Packing List llevan más de dos días sin avanzar en su proceso.</p>");
        AgregarSeccion(html, "Pendientes de recolección", recoleccion);
        AgregarSeccion(html, "Pendientes de embalaje", embalaje);
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static void AgregarSeccion(
        StringBuilder html,
        string titulo,
        List<PackingListPendiente> registros)
    {
        html.AppendLine($"<h2 style='font-size:21px;margin:28px 0 8px 0;'>{WebUtility.HtmlEncode(titulo)}</h2>");
        if (registros.Count == 0)
        {
            html.AppendLine("<p><em>No se encontraron registros.</em></p>");
            return;
        }

        html.AppendLine("<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:15px;'>");
        html.AppendLine("<tr>");
        AgregarEncabezado(html, "Packing List", "20%", "center");
        AgregarEncabezado(html, "Estado", "35%");
        AgregarEncabezado(html, "Fecha de creación", "25%", "center");
        AgregarEncabezado(html, "Días transcurridos", "20%", "center");
        html.AppendLine("</tr>");

        foreach (var item in registros)
        {
            html.AppendLine("<tr>");
            AgregarCelda(html, item.PackingListId.ToString(), "center");
            AgregarCelda(html, item.EstadoDescripcion);
            AgregarCelda(html, item.FechaCreacion.ToString("dd-MM-yyyy HH:mm"), "center");
            AgregarCelda(html, item.DiasTranscurridos.ToString(), "center");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
    }

    private static void AgregarEncabezado(
        StringBuilder html,
        string texto,
        string ancho,
        string alineacion = "left")
    {
        html.AppendLine($"<th align='{alineacion}' width='{ancho}' style='padding:10px 8px;border-bottom:2px solid #dadce0;font-weight:600;'>{WebUtility.HtmlEncode(texto)}</th>");
    }

    private static void AgregarCelda(
        StringBuilder html,
        string texto,
        string alineacion = "left")
    {
        html.AppendLine($"<td align='{alineacion}' valign='top' style='padding:13px 8px;border-bottom:1px solid #eeeeee;'>{WebUtility.HtmlEncode(texto)}</td>");
    }
}
