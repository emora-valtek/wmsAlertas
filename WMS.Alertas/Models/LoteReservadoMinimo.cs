namespace WMS.Alertas.Models;

public class LoteReservadoMinimo
{
    public int SolicitudLoteReservadoId { get; set; }
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
