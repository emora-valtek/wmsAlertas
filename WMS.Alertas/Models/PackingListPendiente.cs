namespace WMS.Alertas.Models;

public sealed class PackingListPendiente
{
    public int PackingListId { get; set; }
    public int Estado { get; set; }
    public string EstadoDescripcion { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public int DiasTranscurridos { get; set; }
    public string Etapa { get; set; } = string.Empty;
}
