namespace WMS.Alertas.Models;

public class AlertaPackingListPendiente
{
    public long AlertaPackingListId { get; set; }
    public Guid EventoId { get; set; }
    public int PackingListId { get; set; }
    public int? DetallePackingListId { get; set; }
    public string TipoModificacion { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
    public int? LoteAnteriorId { get; set; }
    public int? LoteNuevoId { get; set; }
    public decimal? CantidadAnterior { get; set; }
    public decimal? CantidadNueva { get; set; }
    public decimal? CantidadModificada { get; set; }
    public string? Observacion { get; set; }
    public int? NumeroNotaVenta { get; set; }
    public string? ProductoCodigo { get; set; }
    public string? ProductoNombre { get; set; }
    public string? LoteAnteriorCodigo { get; set; }
    public string? LoteNuevoCodigo { get; set; }
    public int? VendedorId { get; set; }
    public string? VendedorNombre { get; set; }
    public string? CorreoVendedor { get; set; }
    public int ModificadoPor { get; set; }
    public string? ModificadoPorNombre { get; set; }
    public DateTime FechaEvento { get; set; }
    public int Intentos { get; set; }
}
