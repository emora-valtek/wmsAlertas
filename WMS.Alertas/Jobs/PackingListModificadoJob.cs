using System.Globalization;
using System.Net;
using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public class PackingListModificadoJob
{
    private const string TipoAlertaLog = "PACKING_LIST_MODIFICADO";
    private const int CantidadMaxima = 50;
    private const int MaximoIntentos = 5;
    private const int ReintentarEnMinutos = 5;
    private const int MinutosExpiracion = 60;

    private readonly AlertaPackingListService _alertaService;
    private readonly CorreoService _correoService;
    private readonly IAlertaEjecucionLogService _logService;

    public PackingListModificadoJob(
        AlertaPackingListService alertaService,
        CorreoService correoService,
        IAlertaEjecucionLogService logService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _logService = logService;
    }

    public async Task Ejecutar()
    {
        var logId = 0;
        var procesadoPor = Guid.NewGuid();

        try
        {
            //busca si quedó por error algun item en estado procesando y la vuelve a pendiente
            var cantidadLiberada =
                await _alertaService.LiberarProcesamientosExpirados(
                    MinutosExpiracion);

            //toma las alertas pendientes 
            var alertas = await _alertaService.TomarPendientes(
                procesadoPor,
                CantidadMaxima);

            if (alertas.Count == 0)
            {
                return;
            }

            logId = await _logService.Iniciar(TipoAlertaLog);

            var enviadas = 0;
            var errores = new List<string>();
            var destinatarios = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var grupos = alertas.GroupBy(x => new
            {
                x.PackingListId,
                x.VendedorId,
                CorreoVendedor = x.CorreoVendedor?.Trim(),
                VendedorNombre = x.VendedorNombre?.Trim()
            });

            foreach (var grupo in grupos)
            {
                var items = grupo
                    .OrderBy(x => x.FechaEvento)
                    .ThenBy(x => x.AlertaPackingListId)
                    .ToList();

                try
                {
                    if (string.IsNullOrWhiteSpace(grupo.Key.CorreoVendedor))
                    {
                        throw new InvalidOperationException(
                            $"El vendedor del Packing List {grupo.Key.PackingListId} no tiene correo configurado.");
                    }

                    var correo = grupo.Key.CorreoVendedor;
                    var html = ArmarHtml(
                        grupo.Key.PackingListId,
                        items);

                    await _correoService.EnviarCorreo(
                        new List<string> { correo },
                        $"Aviso WMS - Packing List {grupo.Key.PackingListId} modificado",
                        html);

                    destinatarios.Add(correo);

                    foreach (var alerta in items)
                    {
                        try
                        {
                            await _alertaService.MarcarEnviada(
                                alerta.AlertaPackingListId,
                                procesadoPor);

                            enviadas++;
                        }
                        catch (Exception ex)
                        {
                            errores.Add(
                                $"Alerta {alerta.AlertaPackingListId}: el correo fue enviado, pero no se pudo marcar ENVIADA. {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (var alerta in items)
                    {
                        await RegistrarError(
                            alerta,
                            procesadoPor,
                            ex,
                            errores);
                    }
                }
            }

            if (errores.Count > 0)
            {
                await _logService.FinalizarError(
                    logId,
                    CrearResumenError(
                        alertas.Count,
                        enviadas,
                        cantidadLiberada,
                        errores));

                return;
            }

            var detalleLog =
                $"{string.Join(";", destinatarios)}; liberadas: {cantidadLiberada}";

            await _logService.FinalizarOk(
                logId,
                enviadas,
                detalleLog);
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

    private async Task RegistrarError(
        AlertaPackingListPendiente alerta,
        Guid procesadoPor,
        Exception error,
        List<string> errores)
    {
        var mensaje =
            $"No se pudo enviar la alerta {alerta.AlertaPackingListId} del Packing List {alerta.PackingListId}. {error}";

        try
        {
            await _alertaService.MarcarError(
                alerta.AlertaPackingListId,
                procesadoPor,
                mensaje,
                MaximoIntentos,
                ReintentarEnMinutos);

            errores.Add(
                $"Alerta {alerta.AlertaPackingListId}: {error.Message}");
        }
        catch (Exception ex)
        {
            errores.Add(
                $"Alerta {alerta.AlertaPackingListId}: {error.Message} Además, no se pudo registrar el error: {ex.Message}");
        }
    }

    private static string ArmarHtml(
        int packingListId,
        List<AlertaPackingListPendiente> items)
    {
        var html = new StringBuilder();
        var numeroNotaVenta = items
            .Select(x => x.NumeroNotaVenta)
            .FirstOrDefault(x => x.HasValue);
        var fecha = items.Max(x => x.FechaEvento);
        var usuarios = string.Join(
            ", ",
            items
                .Select(x => x.ModificadoPorNombre?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        var observaciones = ObtenerObservaciones(items);

        html.AppendLine(
            "<div style='font-family:Arial,Helvetica,sans-serif;color:#202124;max-width:900px;margin:0 auto;line-height:1.45;'>");
        html.AppendLine(
            $"<h1 style='font-size:28px;font-weight:600;margin:0 0 16px 0;'>Packing List {packingListId} modificado</h1>");
        html.AppendLine(
            "<p style='font-size:17px;margin:0 0 30px 0;'>Se registró una modificación en un Packing List asociado a una Nota de Venta.</p>");

        html.AppendLine(
            "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;border-top:1px solid #dadce0;margin-bottom:34px;font-size:16px;'>");
        AgregarFilaResumen(html, "Packing List", packingListId.ToString());
        AgregarFilaResumen(
            html,
            "Nota de Venta",
            numeroNotaVenta?.ToString() ?? string.Empty);
        AgregarFilaResumen(html, "Fecha", fecha.ToString("dd-MM-yyyy HH:mm"));
        AgregarFilaResumen(html, "Usuario", usuarios);
        html.AppendLine("</table>");

        html.AppendLine(
            "<h2 style='font-size:22px;font-weight:600;margin:0 0 14px 0;'>Detalle de la modificación</h2>");
        html.AppendLine(
            "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:16px;margin-bottom:30px;'>");
        html.AppendLine("<tr>");
        html.AppendLine(
            "<th align='left' width='23%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Modificación</th>");
        html.AppendLine(
            "<th align='left' width='44%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Producto</th>");
        html.AppendLine(
            "<th align='left' width='33%' style='padding:0 0 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Cambio</th>");
        html.AppendLine("</tr>");

        foreach (var item in items)
        {
            var producto = ObtenerProducto(
                item.ProductoCodigo,
                item.ProductoNombre);

            html.AppendLine("<tr>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 14px 16px 0;border-bottom:1px solid #eeeeee;'>{Codificar(FormatearTipoModificacion(item.TipoModificacion))}</td>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 14px 16px 0;border-bottom:1px solid #eeeeee;'>{Codificar(producto)}</td>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 0;border-bottom:1px solid #eeeeee;'>{ArmarCambio(item)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");

        if (observaciones.Count > 0)
        {
            html.AppendLine(
                "<h2 style='font-size:20px;font-weight:600;margin:0 0 10px 0;'>Observaciones</h2>");
            html.AppendLine(
                "<ul style='font-size:16px;margin:0;padding-left:26px;'>");

            foreach (var observacion in observaciones)
            {
                html.AppendLine(
                    $"<li style='padding:3px 0;'>{Codificar(observacion)}</li>");
            }

            html.AppendLine("</ul>");
        }

        html.AppendLine("</div>");

        return html.ToString();
    }

    private static void AgregarFilaResumen(
        StringBuilder html,
        string etiqueta,
        string valor)
    {
        html.AppendLine("<tr>");
        html.AppendLine(
            $"<td width='50%' style='padding:15px 12px 15px 0;border-bottom:1px solid #eeeeee;font-weight:600;'>{Codificar(etiqueta)}</td>");
        html.AppendLine(
            $"<td width='50%' style='padding:15px 0;border-bottom:1px solid #eeeeee;'>{Codificar(valor)}</td>");
        html.AppendLine("</tr>");
    }

    private static string ArmarCambio(AlertaPackingListPendiente item)
    {
        if (item.TipoModificacion == "CAMBIO_LOTE")
        {
            var cantidad = FormatearUnidades(item.CantidadModificada);

            return
                $"<strong>{Codificar(item.LoteAnteriorCodigo)} &#10132; {Codificar(item.LoteNuevoCodigo)}</strong>{cantidad}";
        }

        if (item.TipoModificacion == "BAJA_CANTIDAD")
        {
            var cantidad = FormatearUnidades(
                item.CantidadModificada,
                " dada de baja",
                " dadas de baja");

            return
                $"<strong>{FormatearCantidad(item.CantidadAnterior)} &#10132; {FormatearCantidad(item.CantidadNueva)}</strong>{cantidad}";
        }

        return string.Empty;
    }

    private static string FormatearUnidades(
        decimal? cantidad,
        string singularSufijo = "",
        string pluralSufijo = "")
    {
        if (!cantidad.HasValue)
            return string.Empty;

        var sufijo = cantidad.Value == 1
            ? singularSufijo
            : pluralSufijo;
        var unidad = cantidad.Value == 1 ? "unidad" : "unidades";

        return
            $" ({FormatearCantidad(cantidad)} {unidad}{sufijo})";
    }

    private static List<string> ObtenerObservaciones(
        IEnumerable<AlertaPackingListPendiente> items)
    {
        return items
            .SelectMany(x => (x.Observacion ?? string.Empty)
                .Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            .Select(FormatearObservacion)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatearObservacion(string observacion)
    {
        var texto = observacion.Trim().TrimEnd('.');

        if (texto.Equals(
                "Lote enviado a revisión: Sí",
                StringComparison.OrdinalIgnoreCase))
        {
            return "✓ Lote enviado a revisión.";
        }

        if (texto.Equals(
                "Lote enviado a revisión: No",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Lote no enviado a revisión.";
        }

        if (texto.Equals(
                "Lote sin stock",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Lote anterior sin stock.";
        }

        return string.IsNullOrWhiteSpace(texto)
            ? string.Empty
            : $"{texto}.";
    }

    private static string FormatearTipoModificacion(string tipoModificacion)
    {
        return tipoModificacion switch
        {
            "CAMBIO_LOTE" => "Cambio de lote",
            "BAJA_CANTIDAD" => "Baja de cantidad",
            _ => tipoModificacion
        };
    }

    private static string ObtenerProducto(
        string? codigo,
        string? nombre)
    {
        return string.IsNullOrWhiteSpace(nombre)
            ? codigo ?? string.Empty
            : nombre;
    }

    private static string FormatearCantidad(decimal? cantidad)
    {
        return cantidad?.ToString(
            "0.####",
            CultureInfo.GetCultureInfo("es-CL")) ?? string.Empty;
    }

    private static string Codificar(string? valor)
    {
        return WebUtility.HtmlEncode(valor ?? string.Empty);
    }

    private static string CrearResumenError(
        int cantidadTomada,
        int cantidadEnviada,
        int cantidadLiberada,
        List<string> errores)
    {
        const int maximoDetalle = 10;
        var detalle = string.Join(
            " | ",
            errores.Take(maximoDetalle));

        if (errores.Count > maximoDetalle)
        {
            detalle +=
                $" | Se omitieron {errores.Count - maximoDetalle} errores adicionales.";
        }

        return
            $"Tomadas: {cantidadTomada}; enviadas: {cantidadEnviada}; con error: {errores.Count}; liberadas: {cantidadLiberada}. {detalle}";
    }
}
