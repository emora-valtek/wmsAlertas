using System.Net;
using System.Text;
using Hangfire;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public class LoteReservadoMinimoJob
{
    private const string TipoAlertaLog = "LOTE_RESERVADO_MINIMO";
    private const string TipoCorreoDestino = "LoteReservadoMinimo";

    private readonly AlertaLoteReservadoMinimoService _alertaService;
    private readonly CorreoService _correoService;
    private readonly CorreoDestinoService _correoDestinoService;
    private readonly IAlertaEjecucionLogService _logService;

    public LoteReservadoMinimoJob(
        AlertaLoteReservadoMinimoService alertaService,
        CorreoService correoService,
        CorreoDestinoService correoDestinoService,
        IAlertaEjecucionLogService logService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _correoDestinoService = correoDestinoService;
        _logService = logService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task Ejecutar()
    {
        var logId = 0;

        try
        {
            var lotes = await _alertaService.ObtenerPendientes();

            if (lotes.Count == 0)
                return;

            logId = await _logService.Iniciar(TipoAlertaLog);

            var destinatarios =
                await _correoDestinoService.ObtenerCorreos(
                    TipoCorreoDestino);

            if (destinatarios.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No existen destinatarios activos para la alerta {TipoCorreoDestino}.");
            }

            var html = ArmarHtml(lotes);

            await _correoService.EnviarCorreo(
                destinatarios,
                "Alerta WMS - Lotes reservados con saldo mínimo",
                html);

            await _alertaService.MarcarEnviadas(
                lotes.Select(x => x.SolicitudLoteReservadoId));

            await _logService.FinalizarOk(
                logId,
                lotes.Count,
                string.Join(";", destinatarios));
        }
        catch (Exception ex)
        {
            if (logId == 0)
            {
                logId = await _logService.Iniciar(TipoAlertaLog);
            }

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private static string ArmarHtml(List<LoteReservadoMinimo> lotes)
    {
        var html = new StringBuilder();

        html.AppendLine(
            "<div style='font-family:Arial,Helvetica,sans-serif;color:#202124;max-width:1000px;margin:0 auto;line-height:1.45;'>");
        html.AppendLine(
            "<h1 style='font-size:28px;font-weight:600;margin:0 0 16px 0;'>Lotes reservados con saldo mínimo</h1>");
        html.AppendLine(
            "<p style='font-size:17px;margin:0 0 28px 0;'>Las siguientes reservas vigentes alcanzaron o bajaron de la cantidad mínima configurada.</p>");
        html.AppendLine(
            "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:15px;'>");
        html.AppendLine("<tr>");
        AgregarEncabezado(html, "Cliente", "24%");
        AgregarEncabezado(html, "Producto", "28%");
        AgregarEncabezado(html, "Lote", "12%");
        AgregarEncabezado(html, "Reservada", "9%", "center");
        AgregarEncabezado(html, "Saldo", "9%", "center");
        AgregarEncabezado(html, "Mínimo", "8%", "center");
        AgregarEncabezado(html, "Situación", "10%");
        html.AppendLine("</tr>");

        foreach (var lote in lotes)
        {
            var agotado = lote.CantidadRestante <= 0;
            var colorSituacion = agotado ? "#b3261e" : "#8a4b00";
            var fondoSituacion = agotado ? "#fce8e6" : "#fef7e0";
            var producto = UnirProducto(
                lote.ProductoCodigo,
                lote.ProductoNombre);

            html.AppendLine("<tr>");
            AgregarCelda(html, lote.ClienteNombre);
            AgregarCelda(html, producto);
            AgregarCelda(html, lote.LoteCodigo);
            AgregarCelda(
                html,
                lote.CantidadReservada.ToString(),
                "center");
            AgregarCelda(
                html,
                lote.CantidadRestante.ToString(),
                "center",
                "font-weight:600;");
            AgregarCelda(
                html,
                lote.CantidadMinimaInformar.ToString(),
                "center");
            AgregarCelda(
                html,
                FormatearSituacion(lote.Situacion),
                "left",
                $"font-weight:600;color:{colorSituacion};background-color:{fondoSituacion};");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
        html.AppendLine(
            "<p style='font-size:15px;margin:24px 0 0 0;'>Este aviso se envía una sola vez por cada reserva que alcanza el nivel mínimo.</p>");
        html.AppendLine("</div>");

        return html.ToString();
    }

    private static void AgregarEncabezado(
        StringBuilder html,
        string texto,
        string ancho,
        string alineacion = "left")
    {
        html.AppendLine(
            $"<th align='{alineacion}' width='{ancho}' style='padding:10px 8px;border-bottom:2px solid #dadce0;font-weight:600;'>{Codificar(texto)}</th>");
    }

    private static void AgregarCelda(
        StringBuilder html,
        string? texto,
        string alineacion = "left",
        string estiloAdicional = "")
    {
        html.AppendLine(
            $"<td align='{alineacion}' valign='top' style='padding:13px 8px;border-bottom:1px solid #eeeeee;{estiloAdicional}'>{Codificar(texto)}</td>");
    }

    private static string UnirProducto(
        string codigo,
        string nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return nombre;

        if (string.IsNullOrWhiteSpace(nombre))
            return codigo;

        return $"{codigo} - {nombre}";
    }

    private static string FormatearSituacion(string situacion)
    {
        return situacion switch
        {
            "AGOTADO" => "Agotado",
            "NIVEL_MINIMO" => "Nivel mínimo",
            _ => situacion
        };
    }

    private static string Codificar(string? valor)
    {
        return WebUtility.HtmlEncode(valor ?? string.Empty);
    }
}
