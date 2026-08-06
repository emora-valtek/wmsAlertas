using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

/// <summary>
/// Envía la alerta diaria de productos con stock asignado que aún no tienen
/// Packing List. Los PL devueltos no pertenecen a este proceso: se notifican
/// mediante la cola inmediata de AlertaPackingList.
/// </summary>
public class StockAsignadoSinPLJob
{
    private readonly AlertaStockAsignadoSinPLService _alertaService;
    private readonly CorreoService _correoService;
    private readonly CorreoDestinoService _correoDestinoService;
    private readonly IAlertaEjecucionLogService _logService;

    public StockAsignadoSinPLJob(
        AlertaStockAsignadoSinPLService alertaService,
        CorreoService correoService,
        CorreoDestinoService correoDestinoService,
        IAlertaEjecucionLogService logService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _correoDestinoService = correoDestinoService;
        _logService = logService;
    }

    public async Task Ejecutar()
    {
        var logId = await _logService.Iniciar("STOCK_ASIGNADO_SIN_PL");

        try
        {
            var asignados = await _alertaService.ObtenerStockAsignadoSinPL();

            if (!asignados.Any())
            {
                await _logService.FinalizarOk(logId, 0, "Sin registros para enviar");
                return;
            }

            var grupos = asignados
                .Where(x => !string.IsNullOrWhiteSpace(x.Correo))
                .GroupBy(x => new
                {
                    x.Correo,
                    x.Nombre
                });

            // Cada responsable recibe únicamente las Notas de Venta creadas
            // bajo su responsabilidad y que tengan un correo configurado.
            foreach (var grupo in grupos)
            {
                var html = ArmarHtmlAsignadoSinPL(
                    $"Hola {grupo.Key.Nombre},",
                    "Las siguientes Notas de venta presentan productos con stock asignado sin Packing List generado. Por favor revisa los siguientes casos:",
                    grupo.ToList());

                await _correoService.EnviarCorreo(
                    new List<string> { grupo.Key.Correo! },
                    "Alerta WMS - Stock asignado sin PL",
                    html);
            }

            var correosResumen = await _correoDestinoService.ObtenerCorreos("AsignadoSinPL");

            // Los destinatarios fijos reciben además el consolidado completo,
            // incluyendo la columna que identifica al responsable de cada NV.
            if (correosResumen.Any())
            {
                var htmlResumen = ArmarHtmlAsignadoSinPL(
                    "",
                    "Se encontraron Notas de venta con stock asignado sin Packing List generado. A continuación se muestra el resumen completo:",
                    asignados,
                    incluirResponsable: true);

                await _correoService.EnviarCorreo(
                    correosResumen,
                    "Alerta WMS - Stock asignado sin PL",
                    htmlResumen);
            }

            await _logService.FinalizarOk(
                logId,
                asignados.Count,
                string.Join(";", grupos.Select(x => x.Key.Correo).Concat(correosResumen)));
        }
        catch (Exception ex)
        {
            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private static string ArmarHtmlAsignadoSinPL(
        string saludo,
        string mensaje,
        List<StockAsignadoSinPL> items,
        bool incluirResponsable = false)
    {
        // El mismo constructor sirve para el correo individual y el resumen;
        // la única diferencia es la columna opcional de responsable.
        var html = new StringBuilder();

        html.AppendLine("<h3>Alerta WMS - Stock asignado sin Packing List</h3>");
        html.AppendLine($"<p>{saludo}</p>");
        html.AppendLine($"<p>{mensaje}</p>");

        html.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        html.AppendLine("<tr>");

        if (incluirResponsable)
            html.AppendLine("<th>Responsable NV</th>");

        html.AppendLine("<th>Nota Venta</th>");
        html.AppendLine("<th>Producto</th>");
        html.AppendLine("<th>Lote</th>");
        html.AppendLine("<th>Cantidad Asignada</th>");
        html.AppendLine("<th>Fecha Asignación</th>");
        html.AppendLine("<th>Días Pendiente</th>");
        html.AppendLine("</tr>");

        foreach (var item in items)
        {
            html.AppendLine("<tr>");

            if (incluirResponsable)
                html.AppendLine($"<td>{item.Nombre}</td>");

            html.AppendLine($"<td>{item.NotaVenta}</td>");
            html.AppendLine($"<td>{item.Producto}</td>");
            html.AppendLine($"<td>{item.Lote}</td>");
            html.AppendLine($"<td>{item.CantidadAsignada}</td>");
            html.AppendLine($"<td>{item.FechaAsignacion:dd-MM-yyyy}</td>");
            html.AppendLine($"<td>{item.DiasPendiente}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");

        return html.ToString();
    }
}
