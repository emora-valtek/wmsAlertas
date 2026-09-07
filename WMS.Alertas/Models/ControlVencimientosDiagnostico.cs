namespace WMS.Alertas.Models;

public sealed class ExistenciaVencidaDiagnostico
{
    public int ExistenciaId { get; set; }
    public int Estado { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string LoteCodigo { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }
    public int MinimoVencimiento { get; set; }
    public DateTime FechaEnvioRevision { get; set; }
}

public sealed class SolicitudVigenciaVencidaDiagnostico
{
    public int SolicitudLoteReservadoId { get; set; }
    public int Estado { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string LoteCodigo { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public int DiasVigencia { get; set; }
    public DateTime FechaCaducidad { get; set; }
    public int CantidadRestante { get; set; }
    public int CantidadProductos { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
}

public sealed class SolicitudLotePorVencerDiagnostico
{
    public int SolicitudLoteReservadoId { get; set; }
    public int Estado { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string LoteCodigo { get; set; } = string.Empty;
    public DateTime FechaVencimiento { get; set; }
    public int MaximoVencimiento { get; set; }
    public DateTime FechaInicioAviso { get; set; }
    public int CantidadRestante { get; set; }
    public int CantidadProductos { get; set; }
    public string Situacion { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
}

public sealed class ControlVencimientosDiagnostico
{
    public List<ExistenciaVencidaDiagnostico> ExistenciasVencidas { get; init; } = [];
    public List<SolicitudVigenciaVencidaDiagnostico> SolicitudesVigenciaVencida { get; init; } = [];
    public List<SolicitudLotePorVencerDiagnostico> SolicitudesLote { get; init; } = [];
}

public sealed class ResultadoLoteVencimientos
{
    public int Procesadas { get; set; }
    public int Pendientes { get; set; }
}
