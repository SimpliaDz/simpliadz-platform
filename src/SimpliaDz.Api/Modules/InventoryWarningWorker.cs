using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;

namespace SimpliaDz.Api.Modules;

public sealed class InventoryWarningWorker(IServiceScopeFactory scopes,
    ILogger<InventoryWarningWorker> logger, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                if (tenant.IsModuleEnabled("Inventory"))
                    await scope.ServiceProvider.GetRequiredService<InventoryWarningScanService>()
                        .RunAsync(false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inventory warning scan failed; it will retry on the next tick.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
