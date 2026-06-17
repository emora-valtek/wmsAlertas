namespace WMS.Alertas.Interfaces
{
    public interface IAlertaEjecucionLogService
    {
        Task<int> Iniciar(string tipoAlerta);
        Task FinalizarOk(int id, int cantidadRegistros, string destinatarios);
        Task FinalizarError(int id, string mensajeError);
    }
}
