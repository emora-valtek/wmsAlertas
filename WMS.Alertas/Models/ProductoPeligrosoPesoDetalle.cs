namespace WMS.Alertas.Models;

public sealed class ProductoPeligrosoPesoDetalle
{
    public string Atributo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Ubicacion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal? PesoUnitario { get; set; }
    public int CantidadExistencias { get; set; }
    public decimal PesoTotal { get; set; }
}
