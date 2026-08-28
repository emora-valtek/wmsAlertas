using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using WMS.Alertas.Services;

namespace WMS.Alertas.Filters;

public sealed class JobFailureNotificationFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobFailureNotificationFilter> _logger;

    public JobFailureNotificationFilter(
        IServiceScopeFactory scopeFactory,
        ILogger<JobFailureNotificationFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void OnStateApplied(
        ApplyStateContext context,
        IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notifier = scope.ServiceProvider
                .GetRequiredService<NotificacionErrorService>();
            var proceso =
                $"{context.BackgroundJob.Job.Type.Name}.{context.BackgroundJob.Job.Method.Name}";

            notifier.Notificar(
                    proceso,
                    failedState.Exception.ToString(),
                    context.BackgroundJob.Id)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            // La notificación nunca debe impedir que Hangfire persista el fallo.
            _logger.LogError(
                ex,
                "No fue posible enviar la notificación del job fallido {JobId}.",
                context.BackgroundJob.Id);
        }
    }

    public void OnStateUnapplied(
        ApplyStateContext context,
        IWriteOnlyTransaction transaction)
    {
    }
}
