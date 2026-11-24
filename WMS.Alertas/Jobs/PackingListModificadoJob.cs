using System.Globalization;
using System.Net;
using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Global;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

/// <summary>
/// Procesa la cola transaccional de alertas de Packing List. Cada ejecución
/// reserva filas pendientes, envía un correo por PL/vendedor y actualiza cada
/// alerta individualmente para conservar trazabilidad y permitir reintentos.
/// </summary>
public class PackingListModificadoJob
{
    // Parámetros operacionales de lectura, reintento y recuperación de reservas.
    private const int CantidadMaxima = 50;
    private const int MaximoIntentos = 5;
    private const int ReintentarEnMinutos = 5;
    private const int MinutosExpiracion = 60;

    private readonly AlertaPackingListService _alertaService;
    private readonly CorreoService _correoService;
    private readonly IAlertaEjecucionLogService _logService;
    private readonly NotificacionErrorService _notificacionErrorService;

    public PackingListModificadoJob(
        AlertaPackingListService alertaService,
        CorreoService correoService,
        IAlertaEjecucionLogService logService,
        NotificacionErrorService notificacionErrorService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _logService = logService;
        _notificacionErrorService = notificacionErrorService;
    }

    public async Task Ejecutar()
    {
        await EjecutarInterno(0, "PACKING_LIST_MODIFICADO");
    }

    public async Task EjecutarRespaldo()
    {
        await EjecutarInterno(
            ConfiguracionAlertas.MinutosAntiguedadRespaldoPackingList,
            "PACKING_LIST_MODIFICADO_RESPALDO");
    }

    private async Task EjecutarInterno(
        int minutosAntiguedad,
        string tipoAlertaLog)
    {
        var logId = 0;

        // Identifica de forma única esta ejecución. Los SP solo permiten que
        // quien reservó una alerta pueda marcarla ENVIADA o registrar su error.
        var procesadoPor = Guid.NewGuid();

        try
        {
            // Recupera alertas abandonadas si una ejecución anterior se detuvo
            // después de reservarlas y antes de finalizar su procesamiento.
            var cantidadLiberada =
                await _alertaService.LiberarProcesamientosExpirados(
                    MinutosExpiracion);

            // La reserva se realiza en BD con bloqueo y READPAST para impedir
            // que el endpoint y la recurrencia tomen simultáneamente la misma fila.
            var alertas = await _alertaService.TomarPendientes(
                procesadoPor,
                CantidadMaxima,
                minutosAntiguedad);

            if (alertas.Count == 0)
            {
                return;
            }

            logId = await _logService.Iniciar(tipoAlertaLog);

            var enviadas = 0;
            var errores = new List<string>();
            var erroresDefinitivos = new List<string>();
            var destinatarios = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            // Varias modificaciones pendientes del mismo PL se consolidan en
            // un solo correo para el vendedor responsable de la Nota de Venta.
            // Los avisos sin PL (stock asignado liberado) se agrupan por Nota de Venta.
            var grupos = alertas.GroupBy(x => new
            {
                x.PackingListId,
                DocumentoEgresoId = x.PackingListId.HasValue ? null : x.DocumentoEgresoId,
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
                            $"El vendedor de {DescribirReferencia(items[0])} no tiene correo configurado.");
                    }

                    var correo = grupo.Key.CorreoVendedor;
                    var html = ArmarHtml(
                        grupo.Key.PackingListId,
                        items);

                    await _correoService.EnviarCorreo(
                        new List<string> { correo },
                        ObtenerAsunto(grupo.Key.PackingListId, items),
                        html);

                    destinatarios.Add(correo);

                    // El resultado se actualiza por alerta, no por Packing List:
                    // el mismo PL puede generar nuevos eventos en el futuro.
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
                            var detallePersistencia =
                                $"Alerta {alerta.AlertaPackingListId}: el correo fue enviado, pero no se pudo marcar ENVIADA. {ex.Message}";
                            errores.Add(detallePersistencia);
                            erroresDefinitivos.Add(detallePersistencia);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Un fallo de este grupo no impide procesar otros PL. Cada
                    // fila conserva su propio contador y próximo intento.
                    foreach (var alerta in items)
                    {
                        await RegistrarError(
                            alerta,
                            procesadoPor,
                            ex,
                            errores,
                            erroresDefinitivos);
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

                if (erroresDefinitivos.Count > 0)
                {
                    await _notificacionErrorService.Notificar(
                        tipoAlertaLog,
                        "Las siguientes alertas agotaron sus reintentos: " +
                        string.Join(" | ", erroresDefinitivos));
                }

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
                logId = await _logService.Iniciar(tipoAlertaLog);
            }

            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private async Task RegistrarError(
        AlertaPackingListPendiente alerta,
        Guid procesadoPor,
        Exception error,
        List<string> errores,
        List<string> erroresDefinitivos)
    {
        var mensaje =
            $"No se pudo enviar la alerta {alerta.AlertaPackingListId} de {DescribirReferencia(alerta)}. {error}";

        try
        {
            var esErrorDefinitivo = await _alertaService.MarcarError(
                alerta.AlertaPackingListId,
                procesadoPor,
                mensaje,
                MaximoIntentos,
                ReintentarEnMinutos);

            errores.Add(
                $"Alerta {alerta.AlertaPackingListId}: {error.Message}");

            if (esErrorDefinitivo)
            {
                erroresDefinitivos.Add(
                    $"Alerta {alerta.AlertaPackingListId}, {DescribirReferencia(alerta)}: {error.Message}");
            }
        }
        catch (Exception ex)
        {
            var detallePersistencia =
                $"Alerta {alerta.AlertaPackingListId}: {error.Message} Además, no se pudo registrar el error: {ex.Message}";
            errores.Add(detallePersistencia);
            erroresDefinitivos.Add(detallePersistencia);
        }
    }

    private static string ArmarHtml(
        int? packingListId,
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

        // Los avisos de PL cancelado y de stock liberado no muestran su
        // observación: trae el motivo interno y el supervisor (decisión de
        // Elena, 24-11-2026), por eso solo se leen las de los otros tipos.
        var observaciones = ObtenerObservaciones(
            items.Where(x => !EsPlCancelado(x) && !EsAsignacionCancelada(x)));

        // Una devolución o una cancelación no posee producto, lote ni
        // cantidades: se presentan como evento, sin tabla de detalle.
        var soloDevolucion = items.All(EsDevolucionSac);
        var soloCancelacion = items.All(EsPlCancelado);
        var soloAsignacion = items.All(EsAsignacionCancelada);
        var sinDetalle = soloDevolucion || soloCancelacion;
        var contieneEvento = items.Any(x => EsDevolucionSac(x) || EsPlCancelado(x));
        var titulo = ObtenerTitulo(packingListId, items);
        var introduccion = soloDevolucion
            ? "El Packing List fue devuelto a SAC."
            : soloCancelacion
                ? "Se canceló este Packing List."
                : soloAsignacion
                    ? "Desde bodega se liberó parte del stock asignado a esta Nota de Venta."
                    : contieneEvento
                        ? "Se registraron eventos en este Packing List."
                        : "Se registró una modificación en un Packing List asociado a una Nota de Venta.";

        html.AppendLine(
            "<div style='font-family:Arial,Helvetica,sans-serif;color:#202124;max-width:900px;margin:0 auto;line-height:1.45;'>");
        html.AppendLine(
            $"<h1 style='font-size:28px;font-weight:600;margin:0 0 16px 0;'>{Codificar(titulo)}</h1>");
        html.AppendLine(
            $"<p style='font-size:17px;margin:0 0 30px 0;'>{Codificar(introduccion)}</p>");

        html.AppendLine(
            "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;border-top:1px solid #dadce0;margin-bottom:34px;font-size:16px;'>");
        // El stock liberado no tiene Packing List: se omite la fila.
        if (packingListId.HasValue)
            AgregarFilaResumen(html, "Packing List", packingListId.Value.ToString());
        AgregarFilaResumen(
            html,
            "Nota de Venta",
            numeroNotaVenta?.ToString() ?? string.Empty);
        AgregarFilaResumen(html, "Fecha", fecha.ToString("dd-MM-yyyy HH:mm"));
        AgregarFilaResumen(html, "Usuario", usuarios);
        if (soloCancelacion)
            AgregarFilaResumen(html, "Cancelado desde", "Bodega");
        html.AppendLine("</table>");

        if (soloAsignacion)
        {
            AgregarDetalleAsignacion(html, items);
        }
        else if (!sinDetalle)
        {
            // En grupos mixtos se conserva la tabla para mostrar conjuntamente
            // modificaciones de productos y los eventos del mismo PL.
            var tituloDetalle = contieneEvento
                ? "Detalle de los eventos"
                : "Detalle de la modificación";

            html.AppendLine(
                $"<h2 style='font-size:22px;font-weight:600;margin:0 0 14px 0;'>{Codificar(tituloDetalle)}</h2>");
            html.AppendLine(
                "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:16px;margin-bottom:30px;'>");
            html.AppendLine("<tr>");
            html.AppendLine(
                "<th align='left' width='23%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Evento</th>");
            html.AppendLine(
                "<th align='left' width='44%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Producto</th>");
            html.AppendLine(
                "<th align='left' width='33%' style='padding:0 0 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Cambio</th>");
            html.AppendLine("</tr>");

            foreach (var item in items)
            {
                var producto = EsDevolucionSac(item) || EsPlCancelado(item)
                    ? "—"
                    : ObtenerProducto(
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
        }

        if (observaciones.Count > 0)
        {
            var tituloObservaciones = soloDevolucion
                ? "Motivo de la devolución"
                : "Observaciones";

            html.AppendLine(
                $"<h2 style='font-size:20px;font-weight:600;margin:0 0 10px 0;'>{Codificar(tituloObservaciones)}</h2>");
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

        if (EsAsignacionCancelada(item))
        {
            return ArmarCambioAsignacion(item);
        }

        if (EsDevolucionSac(item))
        {
            return "Requiere gestión de SAC.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Asignado antes ➔ asignado después, con las unidades liberadas.
    /// Ejemplo: «10 ➔ 8 (2 unidades liberadas)».
    /// </summary>
    private static string ArmarCambioAsignacion(AlertaPackingListPendiente item)
    {
        var cantidad = FormatearUnidades(
            item.CantidadModificada,
            " liberada",
            " liberadas");

        return
            $"<strong>{FormatearCantidad(item.CantidadAnterior)} &#10132; {FormatearCantidad(item.CantidadNueva)}</strong>{cantidad}";
    }

    /// <summary>
    /// Tabla del aviso de stock asignado liberado: un renglón por producto y
    /// lote de la Nota de Venta, con lo asignado antes y después.
    /// </summary>
    private static void AgregarDetalleAsignacion(
        StringBuilder html,
        List<AlertaPackingListPendiente> items)
    {
        html.AppendLine(
            "<h2 style='font-size:22px;font-weight:600;margin:0 0 14px 0;'>Detalle</h2>");
        html.AppendLine(
            "<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-size:16px;margin-bottom:30px;'>");
        html.AppendLine("<tr>");
        html.AppendLine(
            "<th align='left' width='44%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Producto</th>");
        html.AppendLine(
            "<th align='left' width='22%' style='padding:0 14px 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Lote</th>");
        html.AppendLine(
            "<th align='left' width='34%' style='padding:0 0 10px 0;border-bottom:1px solid #dadce0;font-weight:600;'>Cambio</th>");
        html.AppendLine("</tr>");

        foreach (var item in items)
        {
            // El código va primero: es como el vendedor identifica el producto.
            var producto = string.IsNullOrWhiteSpace(item.ProductoNombre)
                ? item.ProductoCodigo ?? string.Empty
                : $"{item.ProductoCodigo} · {item.ProductoNombre}";

            html.AppendLine("<tr>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 14px 16px 0;border-bottom:1px solid #eeeeee;'>{Codificar(producto)}</td>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 14px 16px 0;border-bottom:1px solid #eeeeee;'>{Codificar(item.LoteAnteriorCodigo)}</td>");
            html.AppendLine(
                $"<td valign='top' style='padding:16px 0;border-bottom:1px solid #eeeeee;'>{ArmarCambioAsignacion(item)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
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
            "DEVUELTO_A_SAC" => "Devuelto a SAC",
            "PL_CANCELADO" => "Packing List cancelado",
            "ASIGNACION_CANCELADA" => "Stock asignado liberado",
            _ => tipoModificacion
        };
    }

    private static string ObtenerAsunto(
        int? packingListId,
        IEnumerable<AlertaPackingListPendiente> items)
    {
        // El asunto usa la misma regla que el encabezado para distinguir una
        // devolución pura de una modificación o de un grupo mixto de eventos.
        return $"Aviso WMS - {ObtenerTitulo(packingListId, items)}";
    }

    private static string ObtenerTitulo(
        int? packingListId,
        IEnumerable<AlertaPackingListPendiente> items)
    {
        var lista = items.ToList();
        var tipos = lista
            .Select(x => x.TipoModificacion)
            .ToList();

        // Sin PL solo llega el stock asignado liberado: se titula por la NV.
        if (!packingListId.HasValue)
        {
            var numeroNotaVenta = lista
                .Select(x => x.NumeroNotaVenta)
                .FirstOrDefault(x => x.HasValue);

            return $"Stock asignado liberado - NV {numeroNotaVenta}";
        }

        if (tipos.All(x => x == "DEVUELTO_A_SAC"))
            return $"Packing List {packingListId} devuelto a SAC";

        if (tipos.All(x => x == "PL_CANCELADO"))
            return $"Packing List {packingListId} cancelado";

        if (tipos.Any(x => x == "DEVUELTO_A_SAC" || x == "PL_CANCELADO"))
            return $"Packing List {packingListId} con cambios";

        return $"Packing List {packingListId} modificado";
    }

    private static bool EsDevolucionSac(
        AlertaPackingListPendiente item)
    {
        // Tipo persistido por spPackingListDevolverSac mediante el registrar
        // común de AlertaPackingList.
        return item.TipoModificacion == "DEVUELTO_A_SAC";
    }

    private static bool EsPlCancelado(
        AlertaPackingListPendiente item)
    {
        // Lo registra spCancelarPackingListBodega (cancelación desde bodega).
        return item.TipoModificacion == "PL_CANCELADO";
    }

    private static bool EsAsignacionCancelada(
        AlertaPackingListPendiente item)
    {
        // Lo registra spRevisionLiberarEnviar al liberar Stock SAC (sin PL).
        return item.TipoModificacion == "ASIGNACION_CANCELADA";
    }

    /// <summary>
    /// Texto para los mensajes de error y el log: el PL si existe; si no, la NV.
    /// </summary>
    private static string DescribirReferencia(
        AlertaPackingListPendiente item)
    {
        return item.PackingListId.HasValue
            ? $"Packing List {item.PackingListId}"
            : $"Nota de Venta {item.NumeroNotaVenta} (documento {item.DocumentoEgresoId})";
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
