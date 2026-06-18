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
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<PendientesIngreso_PpropiaJob>();
builder.Services.AddScoped<PendientesIngreso_MercaderiaJob>();
builder.Services.AddScoped<PendientesIngreso_TodosJob>();
builder.Services.AddScoped<StockAsignadoSinPLJob>();
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

//para pruebas
//using (var scope = app.Services.CreateScope())
//{
//    var job = scope.ServiceProvider.GetRequiredService<StockAsignadoSinPLJob>();

//    await job.Ejecutar();
//}

////// Ejecuta la alerta de Producción propia: Pendientes de ingreso. De lunes a viernes a las 08:30 AM
RecurringJob.AddOrUpdate<PendientesIngreso_PpropiaJob>(
    "Alerta_PendientesIngreso_ProduccionPropia",
    x => x.Ejecutar(),
    "30 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

////// Ejecuta la alerta de Mercadería: Pendientes de ingreso. De lunes a viernes a las 08:35 AM
RecurringJob.AddOrUpdate<PendientesIngreso_MercaderiaJob>(
    "Alerta_PendientesIngreso_Mercaderia",
    x => x.Ejecutar(),
    "35 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

// Ejecuta la alerta consolidada: Pendientes de ingreso. De lunes a viernes a las 08:40 AM
RecurringJob.AddOrUpdate<PendientesIngreso_TodosJob>(
    "Alerta_PendientesIngreso_Todos",
    x => x.Ejecutar(),
    "40 8 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

////// Ejecuta la alerta de SAC: Productos asignados sin PL. De lunes a viernes a las 09:00 AM
RecurringJob.AddOrUpdate<StockAsignadoSinPLJob>(
    "Alerta_SAC_AsignadoSinPL",
    x => x.Ejecutar(),
    "0 9 * * 1-5",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

app.Run();