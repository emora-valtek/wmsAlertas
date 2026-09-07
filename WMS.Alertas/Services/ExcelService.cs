using ClosedXML.Excel;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class ExcelService
{
    public byte[] GenerarExcelExistenciasRevision(
        IEnumerable<ExistenciaVencidaDiagnostico> existencias)
    {
        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Existencias");
        var encabezados = new[] { "Existencia", "Estado anterior", "Producto ID", "Producto", "Lote ID", "Lote", "Vencimiento", "Mínimo vencimiento", "Fecha envío a revisión" };
        for (var columna = 0; columna < encabezados.Length; columna++)
            hoja.Cell(1, columna + 1).Value = encabezados[columna];

        var fila = 2;
        foreach (var item in existencias)
        {
            hoja.Cell(fila, 1).Value = item.ExistenciaId;
            hoja.Cell(fila, 2).Value = item.Estado;
            hoja.Cell(fila, 3).Value = item.ProductoId;
            hoja.Cell(fila, 4).Value = item.ProductoCodigo;
            hoja.Cell(fila, 5).Value = item.LoteId;
            hoja.Cell(fila, 6).Value = item.LoteCodigo;
            hoja.Cell(fila, 7).Value = item.FechaVencimiento;
            hoja.Cell(fila, 8).Value = item.MinimoVencimiento;
            hoja.Cell(fila, 9).Value = item.FechaEnvioRevision;
            fila++;
        }

        hoja.Row(1).Style.Font.Bold = true;
        hoja.Columns().AdjustToContents();
        if (hoja.RangeUsed() != null) hoja.RangeUsed().SetAutoFilter();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerarExcelPendienteIngreso(
        List<PendienteIngresoDto> resumen,
        List<PendienteIngresoDetalleDto> detalle,
        bool incluirDocumento = false)
    {
        using var workbook = new XLWorkbook();

        #region Hoja Resumen

        var wsResumen = workbook.Worksheets.Add("Resumen");

        var col = 1;

        wsResumen.Cell(1, col++).Value = "N° Ingreso";

        if (incluirDocumento)
            wsResumen.Cell(1, col++).Value = "Documento";

        wsResumen.Cell(1, col++).Value = "Código Producto";
        wsResumen.Cell(1, col++).Value = "Lote";
        wsResumen.Cell(1, col++).Value = "Fecha Creación";
        wsResumen.Cell(1, col++).Value = "Cantidad";
        wsResumen.Cell(1, col++).Value = "Estado";
        wsResumen.Cell(1, col++).Value = "Usuario Creación";
        wsResumen.Cell(1, col++).Value = "Días Sin Ingresar";

        var row = 2;

        foreach (var item in resumen)
        {
            col = 1;

            wsResumen.Cell(row, col++).Value = item.NumeroIngreso;

            if (incluirDocumento)
                wsResumen.Cell(row, col++).Value = item.Documento;

            wsResumen.Cell(row, col++).Value = item.CodigoProducto;
            wsResumen.Cell(row, col++).Value = item.Lote;
            wsResumen.Cell(row, col++).Value = item.FechaCreacion;
            wsResumen.Cell(row, col++).Value = item.CantidadExistencias;
            wsResumen.Cell(row, col++).Value = item.Estado;
            wsResumen.Cell(row, col++).Value = item.UsuarioCreacion;
            wsResumen.Cell(row, col++).Value = item.DiasSinIngresar;

            row++;
        }

        wsResumen.Row(1).Style.Font.Bold = true;
        wsResumen.Columns().AdjustToContents();

        if (wsResumen.RangeUsed() != null)
            wsResumen.RangeUsed().SetAutoFilter();

        #endregion

        #region Hoja Detalle

        var wsDetalle = workbook.Worksheets.Add("Detalle");

        col = 1;

        wsDetalle.Cell(1, col++).Value = "N° Ingreso";

        if (incluirDocumento)
            wsDetalle.Cell(1, col++).Value = "Documento";

        wsDetalle.Cell(1, col++).Value = "Código Producto";
        wsDetalle.Cell(1, col++).Value = "Lote";
        wsDetalle.Cell(1, col++).Value = "Secuencia";
        wsDetalle.Cell(1, col++).Value = "Fecha Creación";
        wsDetalle.Cell(1, col++).Value = "Estado";
        wsDetalle.Cell(1, col++).Value = "Usuario Creación";
        wsDetalle.Cell(1, col++).Value = "Días Sin Ingresar";

        row = 2;

        foreach (var item in detalle)
        {
            col = 1;

            wsDetalle.Cell(row, col++).Value = item.NumeroIngreso;

            if (incluirDocumento)
                wsDetalle.Cell(row, col++).Value = item.Documento;

            wsDetalle.Cell(row, col++).Value = item.CodigoProducto;
            wsDetalle.Cell(row, col++).Value = item.Lote;
            wsDetalle.Cell(row, col++).Value = item.Secuencia;
            wsDetalle.Cell(row, col++).Value = item.FechaCreacion;
            wsDetalle.Cell(row, col++).Value = item.Estado;
            wsDetalle.Cell(row, col++).Value = item.UsuarioCreacion;
            wsDetalle.Cell(row, col++).Value = item.DiasSinIngresar;

            row++;
        }

        wsDetalle.Row(1).Style.Font.Bold = true;
        wsDetalle.Columns().AdjustToContents();

        if (wsDetalle.RangeUsed() != null)
            wsDetalle.RangeUsed().SetAutoFilter();

        #endregion

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}
