using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

/// <summary>
/// Envía la alerta diaria de stock asignado que no avanza a despacho: el que
/// aún no tiene Packing List y, desde el 24-11-2026, el que quedó en un PL
/// devuelto a SAC. La devolución misma se avisa en el momento por la cola de
/// AlertaPackingList; esta alerta la recuerda cada día mientras retenga stock.
/// </summary>
public class StockAsignadoSinPLJob
{
    private const string Asunto = "Alerta WMS - Stock asignado pendiente";

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
                    "Las siguientes Notas de venta tienen stock asignado que no avanza a despacho. Por favor revisa los siguientes casos:",
                    grupo.ToList());

                await _correoService.EnviarCorreo(
                    new List<string> { grupo.Key.Correo! },
                    Asunto,
                    html);
            }

            var correosResumen = await _correoDestinoService.ObtenerCorreos("AsignadoSinPL");

            // Los destinatarios fijos reciben además el consolidado completo,
            // incluyendo la columna que identifica al responsable de cada NV.
            if (correosResumen.Any())
            {
                var htmlResumen = ArmarHtmlAsignadoSinPL(
                    "",
                    "Se encontraron Notas de venta con stock asignado que no avanza a despacho. A continuación se muestra el resumen completo:",
                    asignados,
                    incluirResponsable: true);

                await _correoService.EnviarCorreo(
                    correosResumen,
                    Asunto,
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

        html.AppendLine($"<h3>{Asunto}</h3>");
        html.AppendLine($"<p>{saludo}</p>");
        html.AppendLine($"<p>{mensaje}</p>");

        html.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        html.AppendLine("<tr>");

        if (incluirResponsable)
            html.AppendLine("<th>Responsable NV</th>");

        html.AppendLine("<th>Nota Venta</th>");
        html.AppendLine("<th>Packing List</th>");
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
            html.AppendLine($"<td>{DescribirPackingList(item)}</td>");
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

    // Dice al vendedor qué hacer con cada línea: generar el PL o resolver el devuelto.
    private static string DescribirPackingList(StockAsignadoSinPL item)
    {
        if (!item.PackingListId.HasValue)
            return "Sin PL";

        return item.FechaDevolucion.HasValue
            ? $"{item.PackingListId} (devuelto a SAC el {item.FechaDevolucion:dd-MM-yyyy})"
            : $"{item.PackingListId} (devuelto a SAC)";
    }
}
