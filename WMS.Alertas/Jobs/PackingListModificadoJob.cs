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
        var logId = await _logService.Iniciar(TipoAlertaLog);
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
                await _logService.FinalizarOk(
                    logId,
                    0,
                    cantidadLiberada == 0
                        ? "Sin registros para enviar"
                        : $"Sin registros para enviar; liberadas: {cantidadLiberada}");

                return;
            }

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
                        grupo.Key.VendedorNombre,
                        grupo.Key.PackingListId,
                        items);

                    // Modo de prueba: CorreoService redirige el envío a emora@valtek.cl.
                    await _correoService.EnviarCorreoPrueba(
                        new List<string> { correo },
                        $"Alerta WMS - Packing List {grupo.Key.PackingListId} modificado",
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
        string? vendedorNombre,
        int packingListId,
        List<AlertaPackingListPendiente> items)
    {
        var html = new StringBuilder();
        var saludo = string.IsNullOrWhiteSpace(vendedorNombre)
            ? "Hola,"
            : $"Hola {Codificar(vendedorNombre)},";
        var numeroNotaVenta = items
            .Select(x => x.NumeroNotaVenta)
            .FirstOrDefault(x => x.HasValue);

        html.AppendLine("<h3>Alerta WMS - Packing List modificado</h3>");
        html.AppendLine($"<p>{saludo}</p>");
        html.AppendLine(
            "<p>Se realizaron las siguientes modificaciones en un Packing List asociado a una Nota de Venta bajo tu responsabilidad.</p>");
        html.AppendLine("<ul>");
        html.AppendLine($"<li><strong>Packing List:</strong> {packingListId}</li>");

        if (numeroNotaVenta.HasValue)
        {
            html.AppendLine(
                $"<li><strong>Nota de Venta:</strong> {numeroNotaVenta.Value}</li>");
        }

        html.AppendLine("</ul>");
        html.AppendLine(
            "<table border='1' cellpadding='5' cellspacing='0' style='border-collapse:collapse;'>");
        html.AppendLine("<tr>");
        html.AppendLine("<th>Fecha</th>");
        html.AppendLine("<th>Modificación</th>");
        html.AppendLine("<th>Producto</th>");
        html.AppendLine("<th>Lote anterior</th>");
        html.AppendLine("<th>Lote nuevo</th>");
        html.AppendLine("<th>Cantidad anterior</th>");
        html.AppendLine("<th>Cantidad nueva</th>");
        html.AppendLine("<th>Cantidad modificada</th>");
        html.AppendLine("<th>Observación</th>");
        html.AppendLine("<th>Modificado por</th>");
        html.AppendLine("</tr>");

        foreach (var item in items)
        {
            var producto = UnirProducto(
                item.ProductoCodigo,
                item.ProductoNombre);

            html.AppendLine("<tr>");
            html.AppendLine(
                $"<td>{item.FechaEvento:dd-MM-yyyy HH:mm}</td>");
            html.AppendLine(
                $"<td>{Codificar(FormatearTipoModificacion(item.TipoModificacion))}</td>");
            html.AppendLine($"<td>{Codificar(producto)}</td>");
            html.AppendLine(
                $"<td>{Codificar(item.LoteAnteriorCodigo)}</td>");
            html.AppendLine(
                $"<td>{Codificar(item.LoteNuevoCodigo)}</td>");
            html.AppendLine(
                $"<td>{FormatearCantidad(item.CantidadAnterior)}</td>");
            html.AppendLine(
                $"<td>{FormatearCantidad(item.CantidadNueva)}</td>");
            html.AppendLine(
                $"<td>{FormatearCantidad(item.CantidadModificada)}</td>");
            html.AppendLine($"<td>{Codificar(item.Observacion)}</td>");
            html.AppendLine(
                $"<td>{Codificar(item.ModificadoPorNombre)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
        html.AppendLine(
            "<p>Por favor revisa el Packing List y continúa con la gestión correspondiente.</p>");

        return html.ToString();
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

    private static string UnirProducto(
        string? codigo,
        string? nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return nombre ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nombre))
            return codigo;

        return $"{codigo} - {nombre}";
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
