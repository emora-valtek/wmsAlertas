namespace WMS.Alertas.Models;

/// <summary>
/// Instantánea de una alerta reservada para envío. Los datos del vendedor,
/// producto, lotes y usuario quedan almacenados al producirse el evento para
/// que el correo no dependa de cambios posteriores en tablas operacionales.
/// </summary>
public class AlertaPackingListPendiente
{
    // Identificación del evento y de la reserva lógica.
    public long AlertaPackingListId { get; set; }
    public Guid EventoId { get; set; }
    public int PackingListId { get; set; }
    public int? DetallePackingListId { get; set; }
    public string TipoModificacion { get; set; } = string.Empty;

    // Detalle opcional: una devolución a SAC no requiere producto ni cantidades.
    public int? ProductoId { get; set; }
    public int? LoteAnteriorId { get; set; }
    public int? LoteNuevoId { get; set; }
    public decimal? CantidadAnterior { get; set; }
    public decimal? CantidadNueva { get; set; }
    public decimal? CantidadModificada { get; set; }
    public string? Observacion { get; set; }

    // Instantánea de datos descriptivos y del destinatario.
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

    // Control operativo del evento reservado.
    public DateTime FechaEvento { get; set; }
    public int Intentos { get; set; }
}
