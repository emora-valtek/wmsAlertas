using System.Text;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Services;

namespace WMS.Alertas.Jobs;

public class PendientesIngreso_MercaderiaJob
{
    private readonly AlertaPendienteIngreso_MercaderiaService _alertaService;
    private readonly CorreoService _correoService;
    private readonly ExcelService _excelService;
    private readonly CorreoDestinoService _correoDestinoService;
    private readonly IAlertaEjecucionLogService _logService;

    public PendientesIngreso_MercaderiaJob(
        AlertaPendienteIngreso_MercaderiaService alertaService,
        CorreoService correoService, ExcelService excelService, CorreoDestinoService correoDestinoService, IAlertaEjecucionLogService alertaEjecucionLogService)
    {
        _alertaService = alertaService;
        _correoService = correoService;
        _excelService = excelService;
        _correoDestinoService = correoDestinoService;
        _logService = alertaEjecucionLogService;
    }

    public async Task Ejecutar()
    {
        var logId = await _logService.Iniciar(
        "PENDIENTE_INGRESO_MERCADERIA");

        try
        {
            var pendientes = await _alertaService.ObtenerPendientesIngresoMercaderia();

            if (!pendientes.Any()) {
                await _logService.FinalizarOk(
                    logId,
                    0,
                    "Sin registros para enviar");
                return;
            }

            var html = new StringBuilder();

            html.AppendLine("<h3>Alerta WMS - Existencias Pendientes de ingreso</h3>");
            html.AppendLine("<p>Se encontraron existencias Pendientes de ingreso con más de 5 días.</p>");

            html.AppendLine("<table border='1' cellpadding='5' cellspacing='0'>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Ingreso</th>");
            html.AppendLine("<th>Documento</th>");
            html.AppendLine("<th>Producto</th>");
            html.AppendLine("<th>Lote</th>");
            html.AppendLine("<th>Fecha creación</th>");
            html.AppendLine("<th>Cantidad</th>");
            html.AppendLine("<th>Estado</th>");
            html.AppendLine("<th>Días pendiente</th>");
            html.AppendLine("</tr>");

            foreach (var item in pendientes)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{item.NumeroIngreso}</td>");
                html.AppendLine($"<td>{item.Documento}</td>");
                html.AppendLine($"<td>{item.CodigoProducto}</td>");
                html.AppendLine($"<td>{item.Lote}</td>");
                html.AppendLine($"<td>{item.FechaCreacion:dd-MM-yyyy}</td>");
                html.AppendLine($"<td>{item.CantidadExistencias}</td>");
                html.AppendLine($"<td>{item.Estado}</td>");
                html.AppendLine($"<td>{item.DiasSinIngresar}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");

            var detalle = await _alertaService.ObtenerPendientesIngresoDetalleMercaderia();
            var destinatarios = await _correoDestinoService.ObtenerCorreos("PendienteIngresoMercaderia");

            var excelBytes = _excelService.GenerarExcelPendienteIngreso(
                pendientes,
                detalle, true);

            await _correoService.EnviarCorreo(
                destinatarios,
                "Alerta WMS - Existencias Pendientes de ingreso",
                html.ToString(),
                excelBytes,
                $"WMS_PendientesIngreso_{DateTime.Now:yyyyMMdd}.xlsx");

            await _logService.FinalizarOk(
                logId,
                pendientes.Count,
                string.Join(";", destinatarios));

        }
        catch (Exception ex)
        {

            await _logService.FinalizarError(
             logId,
             ex.ToString());

            throw;
        }

        

    }


}
