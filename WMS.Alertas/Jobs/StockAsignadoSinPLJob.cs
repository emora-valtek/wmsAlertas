using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Models;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

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
            var pendientes = await _alertaService.ObtenerPendientesGestionSac();

            if (pendientes.CantidadTotal == 0)
            {
                await _logService.FinalizarOk(logId, 0, "Sin registros para enviar");
                return;
            }

            var responsables = pendientes.StockSinPackingList
                .Select(x => new { x.Correo, x.Nombre })
                .Concat(pendientes.PackingListsDevueltos.Select(x => new { x.Correo, x.Nombre }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Correo))
                .GroupBy(x => new { x.Correo, x.Nombre })
                .Select(x => x.Key)
                .ToList();

            foreach (var responsable in responsables)
            {
                var stockSinPackingList = pendientes.StockSinPackingList
                    .Where(x => x.Correo == responsable.Correo && x.Nombre == responsable.Nombre)
                    .ToList();

                var packingListsDevueltos = pendientes.PackingListsDevueltos
                    .Where(x => x.Correo == responsable.Correo && x.Nombre == responsable.Nombre)
                    .ToList();

                var html = ArmarHtmlPendientesGestionSac(
                    $"Hola {responsable.Nombre},",
                    "Los siguientes casos requieren atención por parte de SAC:",
                    stockSinPackingList,
                    packingListsDevueltos);

                await _correoService.EnviarCorreo(
                    new List<string> { responsable.Correo! },
                    "Alerta WMS - Casos pendientes para SAC",
                    html);
            }

            var correosResumen = await _correoDestinoService.ObtenerCorreos("AsignadoSinPL");

            if (correosResumen.Any())
            {
                var htmlResumen = ArmarHtmlPendientesGestionSac(
                    "",
                    "Se detectaron los siguientes casos que requieren atención por parte de SAC. A continuación, se presenta el resumen:",
                    pendientes.StockSinPackingList,
                    pendientes.PackingListsDevueltos,
                    incluirResponsable: true);

                await _correoService.EnviarCorreo(
                    correosResumen,
                    "Alerta WMS - Casos pendientes para SAC",
                    htmlResumen);
            }

            await _logService.FinalizarOk(
                logId,
                pendientes.CantidadTotal,
                string.Join(";", responsables.Select(x => x.Correo).Concat(correosResumen)));
        }
        catch (Exception ex)
        {
            await _logService.FinalizarError(logId, ex.ToString());
            throw;
        }
    }

    private static string ArmarHtmlPendientesGestionSac(
        string saludo,
        string mensaje,
        List<StockAsignadoSinPL> stockSinPackingList,
        List<PackingListDevueltoSac> packingListsDevueltos,
        bool incluirResponsable = false)
    {
        var html = new StringBuilder();

        html.AppendLine("<h3>Alerta WMS - Casos pendientes para SAC</h3>");

        if (!string.IsNullOrWhiteSpace(saludo))
            html.AppendLine($"<p>{saludo}</p>");

        html.AppendLine($"<p>{mensaje}</p>");

        if (stockSinPackingList.Any())
            AgregarTablaStockSinPackingList(html, stockSinPackingList, incluirResponsable);

        if (packingListsDevueltos.Any())
            AgregarTablaPackingListsDevueltos(html, packingListsDevueltos, incluirResponsable);

        return html.ToString();
    }

    private static void AgregarTablaStockSinPackingList(
        StringBuilder html,
        List<StockAsignadoSinPL> items,
        bool incluirResponsable)
    {
        html.AppendLine("<h4>Stock asignado sin Packing List</h4>");
        html.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        html.AppendLine("<tr>");

        if (incluirResponsable)
            html.AppendLine("<th>Responsable NV</th>");

        html.AppendLine("<th>Nota Venta</th>");
        html.AppendLine("<th>Producto</th>");
        html.AppendLine("<th>Lote</th>");
        html.AppendLine("<th>Cantidad asignada</th>");
        html.AppendLine("<th>Fecha asignación</th>");
        html.AppendLine("<th>Días pendiente</th>");
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
    }

    private static void AgregarTablaPackingListsDevueltos(
        StringBuilder html,
        List<PackingListDevueltoSac> items,
        bool incluirResponsable)
    {
        html.AppendLine("<h4>Packing List devueltos de Bodega</h4>");
        html.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
        html.AppendLine("<tr>");

        if (incluirResponsable)
            html.AppendLine("<th>Responsable NV</th>");

        html.AppendLine("<th>Nota Venta</th>");
        html.AppendLine("<th>Packing List</th>");
        html.AppendLine("<th>Fecha devolución</th>");
        html.AppendLine("<th>Observación</th>");
        html.AppendLine("<th>Devuelto por</th>");
        html.AppendLine("</tr>");

        foreach (var item in items)
        {
            html.AppendLine("<tr>");

            if (incluirResponsable)
                html.AppendLine($"<td>{item.Nombre}</td>");

            html.AppendLine($"<td>{item.NotaVenta}</td>");
            html.AppendLine($"<td>{item.PackingListId}</td>");
            html.AppendLine($"<td>{item.FechaDevolucion:dd-MM-yyyy HH:mm}</td>");
            html.AppendLine($"<td>{item.Observacion}</td>");
            html.AppendLine($"<td>{item.DevueltoPor}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
    }
}
