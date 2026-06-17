using Hangfire;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Jobs;
using WMS.Alertas.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<NodoEmailService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<AlertaPendienteIngresoService>();
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<PendientesIngresoJob>();
builder.Services.AddScoped<AlertaCorreoDestinoService>();
builder.Services.AddScoped<IAlertaEjecucionLogService, AlertaEjecucionLogService>();

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

//para pruebas
//using (var scope = app.Services.CreateScope())
//{
//    var job = scope.ServiceProvider.GetRequiredService<PendientesIngresoJob>();

//    await job.Ejecutar();
//}


RecurringJob.AddOrUpdate<PendientesIngresoJob>(
    "alerta-pendientes-ingreso",
    x => x.Ejecutar(),
    Cron.MinuteInterval(30));

app.Run();