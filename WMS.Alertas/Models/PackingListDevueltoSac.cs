namespace WMS.Alertas.Models;

public class PackingListDevueltoSac
{
    public string NotaVenta { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Correo { get; set; }

    public int PackingListId { get; set; }

    public DateTime FechaDevolucion { get; set; }

    public string Observacion { get; set; } = string.Empty;

    public string DevueltoPor { get; set; } = string.Empty;
}
