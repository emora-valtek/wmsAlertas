using Hangfire;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Jobs;
using WMS.Alertas.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<NodoEmailService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<AlertaPendienteIngreso_PpropiaService>();
builder.Services.AddScoped<AlertaPendienteIngreso_MercaderiaService>();
builder.Services.AddScoped<AlertaPendienteIngreso_TodosService>();
builder.Services.AddScoped<AlertaStockAsignadoSinPLService>();
builder.Services.AddScoped<AlertaPackingListService>();
builder.Services.AddScoped<AlertaLoteReservadoMinimoService>();
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<PendientesIngreso_PpropiaJob>();
builder.Services.AddScoped<PendientesIngreso_MercaderiaJob>();
builder.Services.AddScoped<PendientesIngreso_TodosJob>();
builder.Services.AddScoped<StockAsignadoSinPLJob>();
builder.Services.AddScoped<PackingListModificadoJob>();
builder.Services.AddScoped<LoteReservadoMinimoJob>();
builder.Services.AddScoped<CorreoDestinoService>();
builder.Services.AddScoped<IAlertaEjecucionLogService, LogService>();

builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHangfireServer();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseHangfireDashboard("/hangfire");

// Disparador productivo: no recibe el PL ni envía el correo durante la petición.
// Solo encola el job; este consulta y reserva todas las alertas PENDIENTE en BD.
app.MapPost(
        "/api/alertas/packing-list/procesar-pendientes",
        (IBackgroundJobClient backgroundJobs) =>
        {
            var jobId = backgroundJobs.Enqueue<PackingListModificadoJob>(
                job => job.Ejecutar());

            return Results.Accepted(
                value: new
                {
                    JobId = jobId,
                    Mensaje = "Procesamiento de alertas de Packing List encolado."
                });
        })
    .WithName("ProcesarAlertasPackingListPendientes")
    .WithSummary("Encola el procesamiento de alertas pendientes de Packing List")
    .Produces(StatusCodes.Status202Accepted);

// Endpoint seguro para validar conectividad desde QA. No consulta la cola,
// no encola jobs y no envía correos; únicamente deja evidencia en el log.
app.MapPost(
        "/api/alertas/packing-list/prueba-conexion",
        async (
            HttpContext context,
            IAlertaEjecucionLogService logService) =>
        {
            var logId = await logService.Iniciar(
                "PRUEBA_ENDPOINT_PACKING_LIST");
            var direccionOrigen =
                context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

            await logService.FinalizarOk(
                logId,
                0,
                $"Llamada de prueba recibida. Origen: {direccionOrigen}");

            return Results.Ok(new
            {
                LogId = logId,
                Mensaje = "Llamada de prueba registrada correctamente."
            });
        })
    .WithName("ProbarConexionAlertasPackingList")
    .WithSummary("Registra una llamada de prueba sin procesar alertas")
    .Produces(StatusCodes.Status200OK);

// Se eliminan primero las definiciones persistidas para que Hangfire no conserve
// programaciones antiguas cuando cambian los horarios o se deshabilita un job.
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_ProduccionPropia");
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_Mercaderia");
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_Todos");
RecurringJob.RemoveIfExists("Alerta_SAC_AsignadoSinPL");
RecurringJob.RemoveIfExists("Alerta_PackingList_Modificado");
RecurringJob.RemoveIfExists("Alerta_LoteReservado_Minimo");

// Producción propia: pendientes de ingreso, lunes a viernes a las 08:30.
RecurringJob.AddOrUpdate<PendientesIngreso_PpropiaJob>(
    "Alerta_PendientesIngreso_ProduccionPropia",
    x => x.Ejecutar(),
    "30 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// Mercadería: pendientes de ingreso, lunes a viernes a las 08:35.
RecurringJob.AddOrUpdate<PendientesIngreso_MercaderiaJob>(
    "Alerta_PendientesIngreso_Mercaderia",
    x => x.Ejecutar(),
    "35 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// Consolidado de pendientes de ingreso, lunes a viernes a las 08:40.
RecurringJob.AddOrUpdate<PendientesIngreso_TodosJob>(
    "Alerta_PendientesIngreso_Todos",
    x => x.Ejecutar(),
    "40 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// Stock asignado sin Packing List, lunes a viernes a las 09:00.
RecurringJob.AddOrUpdate<StockAsignadoSinPLJob>(
    "Alerta_SAC_AsignadoSinPL",
    x => x.Ejecutar(),
    "0 9 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// Lotes reservados con saldo mínimo, lunes a viernes a las 09:05.
RecurringJob.AddOrUpdate<LoteReservadoMinimoJob>(
    "Alerta_LoteReservado_Minimo",
    x => x.Ejecutar(),
    "5 9 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// DESHABILITADO: las alertas de modificación de Packing List ahora se
// procesan cuando WMS llama al endpoint /api/alertas/packing-list/procesar-pendientes.
// Se conserva esta definición comentada solamente como referencia por si fuera
// necesario restablecer temporalmente la revisión automática cada dos minutos.
//
// RecurringJob.AddOrUpdate<PackingListModificadoJob>(
//     "Alerta_PackingList_Modificado",
//     x => x.Ejecutar(),
//     "*/2 8-17 * * 1-5",
//     new RecurringJobOptions
//     {
//         TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
//     });

app.Run();
