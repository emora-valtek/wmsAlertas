namespace WMS.Alertas.Models;

/// <summary>
/// Representa una reserva que alcanzó el saldo mínimo y contiene también
/// los datos del usuario que la creó, quien debe recibir la notificación.
/// </summary>
public class LoteReservadoMinimo
{
    public int SolicitudLoteReservadoId { get; set; }
    public int SolicitanteId { get; set; }
    public string SolicitanteNombre { get; set; } = string.Empty;
    public string SolicitanteCorreo { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string LoteCodigo { get; set; } = string.Empty;
    public int CantidadReservada { get; set; }
    public int CantidadRestante { get; set; }
    public int CantidadMinimaInformar { get; set; }
    public string Situacion { get; set; } = string.Empty;
}
