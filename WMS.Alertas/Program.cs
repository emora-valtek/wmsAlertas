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

// Modificaciones de Packing List cada dos minutos, de lunes a viernes
// entre las 08:00 y las 17:58.
RecurringJob.AddOrUpdate<PackingListModificadoJob>(
    "Alerta_PackingList_Modificado",
    x => x.Ejecutar(),
    "*/2 8-17 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

app.Run();
