namespace WMS.Alertas.Models
{
    public class StockAsignadoSinPL
    {
        public string NotaVenta { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
        public string? Correo { get; set; }

        public string Producto { get; set; } = string.Empty;

        public string Lote { get; set; } = string.Empty;

        public int CantidadAsignada { get; set; }

        public DateTime FechaAsignacion { get; set; }

        public int DiasPendiente { get; set; }
    }
}
