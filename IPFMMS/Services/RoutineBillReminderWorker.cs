using IPFMMS.Data;
using Microsoft.EntityFrameworkCore;
namespace IPFMMS.Services;
public sealed class RoutineBillReminderWorker(IServiceScopeFactory scopes,IConfiguration config,ILogger<RoutineBillReminderWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!config.GetValue<bool>("RoutineBills:RemindersEnabled"))return;
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date;
                for(int page=0;!stoppingToken.IsCancellationRequested;page++)
                {
                    using var batch=scopes.CreateScope();
                    var db=batch.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var owners=await db.RoutineBills.AsNoTracking().Where(b=>b.status=="Active"&&b.dueDate<=today.AddDays(b.reminderPeriod)
                        && b.User!.AccountStatus==IPFMMS.Models.AccountStatus.Active&&(b.User.Role=="User"||b.User.Role=="Parent"))
                        .Select(b=>b.userId).Distinct().OrderBy(id=>id).Skip(page*20).Take(20).ToListAsync(stoppingToken);
                    if(owners.Count==0)break;
                    foreach(var owner in owners)
                    {
                        try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<RoutineBillReminderService>().RefreshAsync(owner,today,stoppingToken);}
                        catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
                        catch(Exception ex){logger.LogError(ex,"Bill reminder check failed; a later check will retry.");}
                    }
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception ex){logger.LogError(ex,"Bill reminder worker failed; a later check will retry.");}
        }while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
