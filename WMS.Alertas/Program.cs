using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WMS.Alertas.Filters;
using WMS.Alertas.Global;
using WMS.Alertas.Health;
using WMS.Alertas.Interfaces;
using WMS.Alertas.Jobs;
using WMS.Alertas.Security;
using WMS.Alertas.Services;

var builder = WebApplication.CreateBuilder(args);

var esProduccion = builder.Environment.IsProduction();
var modoConfigurado = builder.Configuration["Alertas:Modo"];

if (!Enum.TryParse<ModoEjecucionAlertas>(
        modoConfigurado,
        ignoreCase: true,
        out var modoEjecucion))
{
    throw new InvalidOperationException(
        "Alertas__Modo debe ser Deshabilitado, Manual o Automatico.");
}

if (!esProduccion && modoEjecucion == ModoEjecucionAlertas.Automatico)
{
    throw new InvalidOperationException(
        "La programación automática de alertas solo puede habilitarse en producción.");
}

var correoPruebas =
    builder.Configuration["Alertas:CorreoPruebas"]?.Trim();
var usarCorreoPruebas =
    builder.Configuration.GetValue<bool>("Alertas:UsarCorreoPruebas");

if (modoEjecucion != ModoEjecucionAlertas.Deshabilitado &&
    !esProduccion &&
    usarCorreoPruebas &&
    string.IsNullOrWhiteSpace(correoPruebas))
{
    throw new InvalidOperationException(
        "Para habilitar alertas fuera de producción se debe configurar " +
        "Alertas__CorreoPruebas.");
}

if (modoEjecucion != ModoEjecucionAlertas.Deshabilitado &&
    !esProduccion &&
    usarCorreoPruebas)
{
    try
    {
        _ = new System.Net.Mail.MailAddress(correoPruebas!);
    }
    catch (FormatException ex)
    {
        throw new InvalidOperationException(
            "Alertas__CorreoPruebas no contiene una dirección válida.",
            ex);
    }
}

var configuracionEjecucion = new ConfiguracionEjecucionAlertas(
    modoEjecucion,
    esProduccion,
    builder.Environment.EnvironmentName,
    correoPruebas,
    usarCorreoPruebas);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(configuracionEjecucion);

builder.Services.AddScoped<NodoEmailService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<AlertaPendienteIngreso_PpropiaService>();
builder.Services.AddScoped<AlertaPendienteIngreso_MercaderiaService>();
builder.Services.AddScoped<AlertaPendienteIngreso_TodosService>();
builder.Services.AddScoped<AlertaStockAsignadoSinPLService>();
builder.Services.AddScoped<AlertaPackingListService>();
builder.Services.AddScoped<AlertaLoteReservadoMinimoService>();
builder.Services.AddScoped<ControlVencimientosService>();
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<PendientesIngreso_PpropiaJob>();
builder.Services.AddScoped<PendientesIngreso_MercaderiaJob>();
builder.Services.AddScoped<PendientesIngreso_TodosJob>();
builder.Services.AddScoped<StockAsignadoSinPLJob>();
builder.Services.AddScoped<PackingListModificadoJob>();
builder.Services.AddScoped<LoteReservadoMinimoJob>();
builder.Services.AddScoped<ControlVencimientosJob>();
builder.Services.AddScoped<CorreoDestinoService>();
builder.Services.AddScoped<IAlertaEjecucionLogService, LogService>();
builder.Services.AddScoped<NotificacionErrorService>();
builder.Services.AddSingleton<ApiKeyEndpointFilter>();
builder.Services.AddSingleton<JobFailureNotificationFilter>();
builder.Services.Configure<ApiSecurityOptions>(
    builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
builder.Services.AddHealthChecks()
    .AddCheck<WmsAlertasHealthCheck>("wms_alertas");

builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

if (configuracionEjecucion.Habilitadas)
{
    if (configuracionEjecucion.EjecucionManual)
    {
        builder.Services.AddHangfireServer(options =>
        {
            options.Queues = new[] { ConfiguracionAlertas.ColaManualQa };
        });
    }
    else
    {
        builder.Services.AddHangfireServer();
    }
}


var app = builder.Build();

app.Logger.LogInformation(
    "WMS.Alertas inició en {Ambiente} con modo {Modo} y correo de pruebas {CorreoPruebas}.",
    configuracionEjecucion.Ambiente,
    configuracionEjecucion.Modo,
    configuracionEjecucion.UsarCorreoPruebas);

GlobalJobFilters.Filters.Add(
    app.Services.GetRequiredService<JobFailureNotificationFilter>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Sin filtros personalizados, Hangfire permite el dashboard únicamente a
// solicitudes locales. Se conserva así para que solo el encargado pueda verlo
// desde el propio servidor.
app.UseHangfireDashboard("/hangfire");

var apiPackingList = app
    .MapGroup("/api/alertas/packing-list")
    .AddEndpointFilter<ApiKeyEndpointFilter>();

// Disparador productivo: no recibe el PL ni envía el correo durante la petición.
// Solo encola el job; este consulta y reserva todas las alertas PENDIENTE en BD.
apiPackingList.MapPost(
        "/procesar-pendientes",
        (IBackgroundJobClient backgroundJobs) =>
        {
            if (!configuracionEjecucion.ProgramacionAutomatica)
            {
                return Results.Accepted(
                    value: new
                    {
                        JobId = (string?)null,
                        Mensaje =
                            "Procesamiento automático deshabilitado en este ambiente. " +
                            "No se encoló ningún procesamiento."
                    });
            }

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
apiPackingList.MapPost(
        "/prueba-conexion",
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

app.MapGet(
        "/health",
        async (
            HealthCheckService healthCheckService,
            CancellationToken cancellationToken) =>
        {
            var resultado = await healthCheckService.CheckHealthAsync(
                cancellationToken);

            return resultado.Status == HealthStatus.Healthy
                ? Results.Ok(new
                {
                    Estado = "OK",
                    Ambiente = configuracionEjecucion.Ambiente,
                    Modo = configuracionEjecucion.Modo.ToString().ToUpperInvariant(),
                    UsarCorreoPruebas =
                        !configuracionEjecucion.EsProduccion &&
                        configuracionEjecucion.UsarCorreoPruebas
                })
                : Results.Json(
                    new
                    {
                        Estado = "ERROR",
                        Ambiente = configuracionEjecucion.Ambiente,
                        Modo = configuracionEjecucion.Modo.ToString().ToUpperInvariant(),
                        UsarCorreoPruebas =
                            !configuracionEjecucion.EsProduccion &&
                            configuracionEjecucion.UsarCorreoPruebas
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        })
    .AddEndpointFilter<ApiKeyEndpointFilter>()
    .WithName("ComprobarSaludWmsAlertas")
    .WithSummary("Comprueba aplicación, base de datos, Hangfire y jobs diarios")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

// Se eliminan primero las definiciones persistidas para que Hangfire no conserve
// programaciones antiguas cuando cambian los horarios o se deshabilita un job.
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_ProduccionPropia");
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_Mercaderia");
RecurringJob.RemoveIfExists("Alerta_PendientesIngreso_Todos");
RecurringJob.RemoveIfExists("Alerta_SAC_AsignadoSinPL");
RecurringJob.RemoveIfExists("Alerta_PackingList_Modificado");
RecurringJob.RemoveIfExists("Respaldo_PackingList_0915");
RecurringJob.RemoveIfExists("Respaldo_PackingList_1500");
RecurringJob.RemoveIfExists("Alerta_LoteReservado_Minimo");
RecurringJob.RemoveIfExists("Diagnostico_ControlVencimientos");
RecurringJob.RemoveIfExists("ControlVencimientos_EnviarRevision");
RecurringJob.RemoveIfExists("ControlVencimientos_LotesReservados");

if (configuracionEjecucion.Habilitadas)
{
    var cola = configuracionEjecucion.EjecucionManual
        ? ConfiguracionAlertas.ColaManualQa
        : "default";

    string Horario(string cronProduccion) =>
        configuracionEjecucion.ProgramacionAutomatica
            ? cronProduccion
            : Cron.Never();

    RecurringJobOptions Opciones() => new()
    {
        TimeZone = ConfiguracionAlertas.ZonaHorariaChile
    };

    // Producción propia: pendientes de ingreso, lunes a viernes a las 08:30.
    RecurringJob.AddOrUpdate<PendientesIngreso_PpropiaJob>(
        "Alerta_PendientesIngreso_ProduccionPropia",
        cola,
        x => x.Ejecutar(),
        Horario("30 8 * * 1-5"),
        Opciones());

    // Mercadería: pendientes de ingreso, lunes a viernes a las 08:35.
    RecurringJob.AddOrUpdate<PendientesIngreso_MercaderiaJob>(
        "Alerta_PendientesIngreso_Mercaderia",
        cola,
        x => x.Ejecutar(),
        Horario("35 8 * * 1-5"),
        Opciones());

    // Consolidado de pendientes de ingreso, lunes a viernes a las 08:40.
    RecurringJob.AddOrUpdate<PendientesIngreso_TodosJob>(
        "Alerta_PendientesIngreso_Todos",
        cola,
        x => x.Ejecutar(),
        Horario("40 8 * * 1-5"),
        Opciones());

    // Stock asignado sin Packing List, lunes a viernes a las 09:00.
    RecurringJob.AddOrUpdate<StockAsignadoSinPLJob>(
        "Alerta_SAC_AsignadoSinPL",
        cola,
        x => x.Ejecutar(),
        Horario("0 9 * * 1-5"),
        Opciones());

    // Lotes reservados con saldo mínimo, lunes a viernes a las 09:05.
    RecurringJob.AddOrUpdate<LoteReservadoMinimoJob>(
        "Alerta_LoteReservado_Minimo",
        cola,
        x => x.Ejecutar(),
        Horario("5 9 * * 1-5"),
        Opciones());

    // Primera etapa de la migración de portal.Job: consulta los candidatos y
    // registra cantidades, pero no modifica inventario ni solicitudes. Queda
    // sin horario para ejecutarlo manualmente desde el dashboard de Hangfire.
    RecurringJob.AddOrUpdate<ControlVencimientosJob>(
        "Diagnostico_ControlVencimientos",
        cola,
        x => x.EjecutarDiagnostico(),
        Cron.Never(),
        Opciones());

    // Procesamiento real inicialmente manual para una prueba controlada.
    RecurringJob.AddOrUpdate<ControlVencimientosJob>(
        "ControlVencimientos_EnviarRevision",
        cola,
        x => x.EjecutarEnvioRevision(),
        Cron.Never(),
        Opciones());

    RecurringJob.AddOrUpdate<ControlVencimientosJob>(
        "ControlVencimientos_LotesReservados",
        cola,
        x => x.EjecutarLotesReservados(),
        Cron.Never(),
        Opciones());

    // Respaldo del disparo inmediato efectuado por WMS. Solo toma alertas que
    // continúen pendientes después de 15 minutos.
    RecurringJob.AddOrUpdate<PackingListModificadoJob>(
        "Respaldo_PackingList_0915",
        cola,
        x => x.EjecutarRespaldo(),
        Horario("15 9 * * 1-5"),
        Opciones());

    RecurringJob.AddOrUpdate<PackingListModificadoJob>(
        "Respaldo_PackingList_1500",
        cola,
        x => x.EjecutarRespaldo(),
        Horario("0 15 * * 1-5"),
        Opciones());
}

app.Run();
