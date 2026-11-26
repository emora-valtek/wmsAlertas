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

        /// <summary>
        /// Días desde la asignación o, si el stock está en un PL devuelto a SAC, desde la devolución.
        /// </summary>
        public int DiasPendiente { get; set; }

        /// <summary>
        /// PL devuelto a SAC que retiene el stock. NULL cuando el stock está asignado sin PL.
        /// </summary>
        public int? PackingListId { get; set; }

        /// <summary>
        /// Fecha de la devolución a SAC; solo cuando hay PackingListId.
        /// </summary>
        public DateTime? FechaDevolucion { get; set; }
    }
}
