namespace WMS.Alertas.Models;

public sealed record ProductoManager(string Codigo, string Nombre);

public sealed record ResultadoActualizacionProductosManager(
    int Consultados,
    int Creados,
    int NombresActualizados,
    int SinCambios,
    int Errores,
    string DetalleErrores);
