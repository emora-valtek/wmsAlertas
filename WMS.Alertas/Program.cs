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
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<PendientesIngreso_PpropiaJob>();
builder.Services.AddScoped<PendientesIngreso_MercaderiaJob>();
builder.Services.AddScoped<PendientesIngreso_TodosJob>();
builder.Services.AddScoped<StockAsignadoSinPLJob>();
builder.Services.AddScoped<PackingListModificadoJob>();
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

//para pruebas
//using (var scope = app.Services.CreateScope())
//{
//    var job = scope.ServiceProvider.GetRequiredService<StockAsignadoSinPLJob>();

//    await job.Ejecutar();
//}

// Modo de prueba: los demás jobs permanecen eliminados de Hangfire.
// Restaurar sus AddOrUpdate antes de publicar en producción.

// Procesa alertas por modificaciones de Packing List cada dos minutos.
RecurringJob.AddOrUpdate<PackingListModificadoJob>(
    "Alerta_PackingList_Modificado",
    x => x.Ejecutar(),
    "*/2 * * * *",
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    });

app.Run();
