using ClosedXML.Excel;
using WMS.Alertas.Models;

namespace WMS.Alertas.Services;

public class ExcelService
{
    public byte[] GenerarExcelPendienteIngreso(
        List<PendienteIngresoDto> resumen,
        List<PendienteIngresoDetalleDto> detalle)
    {
        using var workbook = new XLWorkbook();

        #region Hoja Resumen

        var wsResumen = workbook.Worksheets.Add("Resumen");

        wsResumen.Cell(1, 1).Value = "N° Ingreso";
        wsResumen.Cell(1, 2).Value = "Código Producto";
        wsResumen.Cell(1, 3).Value = "Lote";
        wsResumen.Cell(1, 4).Value = "Fecha Creación";
        wsResumen.Cell(1, 5).Value = "Cantidad";
        wsResumen.Cell(1, 6).Value = "Estado";
        wsResumen.Cell(1, 7).Value = "Usuario Creación";
        wsResumen.Cell(1, 8).Value = "Días Sin Ingresar";

        var row = 2;

        foreach (var item in resumen)
        {
            wsResumen.Cell(row, 1).Value = item.NumeroIngreso;
            wsResumen.Cell(row, 2).Value = item.CodigoProducto;
            wsResumen.Cell(row, 3).Value = item.Lote;
            wsResumen.Cell(row, 4).Value = item.FechaCreacion;
            wsResumen.Cell(row, 5).Value = item.CantidadExistencias;
            wsResumen.Cell(row, 6).Value = item.Estado;
            wsResumen.Cell(row, 7).Value = item.UsuarioCreacion;
            wsResumen.Cell(row, 8).Value = item.DiasSinIngresar;

            row++;
        }

        wsResumen.Row(1).Style.Font.Bold = true;
        wsResumen.Columns().AdjustToContents();

        if (wsResumen.RangeUsed() != null)
            wsResumen.RangeUsed().SetAutoFilter();

        #endregion

        #region Hoja Detalle

        var wsDetalle = workbook.Worksheets.Add("Detalle");

        wsDetalle.Cell(1, 1).Value = "N° Ingreso";
        wsDetalle.Cell(1, 2).Value = "Código Producto";
        wsDetalle.Cell(1, 3).Value = "Lote";
        wsDetalle.Cell(1, 4).Value = "Secuencia";
        wsDetalle.Cell(1, 5).Value = "Fecha Creación";
        wsDetalle.Cell(1, 6).Value = "Estado";
        wsDetalle.Cell(1, 7).Value = "Usuario Creación";
        wsDetalle.Cell(1, 8).Value = "Días Sin Ingresar";

        row = 2;

        foreach (var item in detalle)
        {
            wsDetalle.Cell(row, 1).Value = item.NumeroIngreso;
            wsDetalle.Cell(row, 2).Value = item.CodigoProducto;
            wsDetalle.Cell(row, 3).Value = item.Lote;
            wsDetalle.Cell(row, 4).Value = item.Secuencia;
            wsDetalle.Cell(row, 5).Value = item.FechaCreacion;
            wsDetalle.Cell(row, 6).Value = item.Estado;
            wsDetalle.Cell(row, 7).Value = item.UsuarioCreacion;
            wsDetalle.Cell(row, 8).Value = item.DiasSinIngresar;

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