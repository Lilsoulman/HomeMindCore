using HomeMind.Business.IServices.Productivity;
using HomeMind.Common.Helpers;
using HomeMind.Common.Infrastructure;
using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
using HomeMind.Common.Repository;
using Microsoft.EntityFrameworkCore;
namespace HomeMind.Business.Services.Productivity;
public sealed class CalendarServices : ICalendarServices
{
    private readonly HomeMindDbContext _db; private readonly SecretProtector _secrets;
    public CalendarServices(HomeMindDbContext db,SecretProtector secrets){_db=db;_secrets=secrets;}
    public async Task<ServiceResult> ListEventsAsync(long userId,long tenantId,DateTime? from,DateTime? to,CancellationToken token=default){var q=_db.CalendarEvents.Where(x=>x.UserId==userId&&x.TenantId==tenantId&&x.DeletedAt==null);if(from is not null)q=q.Where(x=>x.StartAt>=from);if(to is not null)q=q.Where(x=>x.StartAt<=to);var items=await q.OrderBy(x=>x.StartAt).ToListAsync(token);return new(200,"查询成功。",items.Select(EventView));}
    public async Task<ServiceResult> CreateEventAsync(long userId,long tenantId,CalendarEventRequest r,CancellationToken token=default){if(string.IsNullOrWhiteSpace(r.Title)||r.StartAt is null)return new(422,"请填写日程标题和开始时间。");var x=new CalendarEvent{TenantId=tenantId,UserId=userId,Title=r.Title.Trim(),Description=r.Description,Location=r.Location,StartAt=r.StartAt.Value,EndAt=r.EndAt,Timezone=r.Timezone,AllDay=r.AllDay??false,Color=r.Color,Opacity=r.Opacity,RepeatRule=r.RepeatRule};_db.CalendarEvents.Add(x);await _db.SaveChangesAsync(token);return new(201,"创建成功。",EventView(x));}
    public async Task<ServiceResult> UpdateEventAsync(long userId,long tenantId,long id,CalendarEventRequest r,CancellationToken token=default){var x=await EventAsync(userId,tenantId,id,token);if(x is null)return new(404,"请求的资源不存在。");x.Title=r.Title??x.Title;x.Description=r.Description??x.Description;x.Location=r.Location??x.Location;x.StartAt=r.StartAt??x.StartAt;x.EndAt=r.EndAt??x.EndAt;x.Timezone=r.Timezone??x.Timezone;x.AllDay=r.AllDay??x.AllDay;x.Color=r.Color??x.Color;x.Opacity=r.Opacity??x.Opacity;x.RepeatRule=r.RepeatRule??x.RepeatRule;await _db.SaveChangesAsync(token);return new(200,"更新成功。",EventView(x));}
    public async Task<ServiceResult> DeleteEventAsync(long userId,long tenantId,long id,CancellationToken token=default){var x=await EventAsync(userId,tenantId,id,token);if(x is null)return new(404,"请求的资源不存在。");x.DeletedAt=DateTime.UtcNow;await _db.SaveChangesAsync(token);return new(200,"删除成功。",new{id});}
    public async Task<ServiceResult> ListSubscriptionsAsync(long userId,long tenantId,CancellationToken token=default){var x=await _db.CalendarSubscriptions.Where(x=>x.UserId==userId&&x.TenantId==tenantId&&x.DeletedAt==null).OrderByDescending(x=>x.Id).ToListAsync(token);return new(200,"查询成功。",x.Select(x=>new{x.Id,x.Name,x.Enabled,x.RefreshIntervalMin,x.LastFetchAt,x.LastError,x.CreatedAt}));}
    public async Task<ServiceResult> CreateSubscriptionAsync(long userId,long tenantId,SubscriptionRequest r,CancellationToken token=default){if(!Uri.TryCreate(r.Url,UriKind.Absolute,out _))return new(422,"订阅地址必须是绝对 URL。");var x=new CalendarSubscription{TenantId=tenantId,UserId=userId,Name=string.IsNullOrWhiteSpace(r.Name)?"日历订阅":r.Name.Trim(),SourceUrlEncrypted=_secrets.Encrypt(r.Url!),SourceUrlHash=DbValue.Sha256(r.Url!),Enabled=r.Enabled??true,RefreshIntervalMin=r.RefreshIntervalMin??60};_db.CalendarSubscriptions.Add(x);await _db.SaveChangesAsync(token);return new(201,"创建成功。",new{x.Id,x.Name,x.Enabled,x.RefreshIntervalMin,x.CreatedAt});}
    public async Task<ServiceResult> UpdateSubscriptionAsync(long userId,long tenantId,long id,SubscriptionRequest r,CancellationToken token=default){var x=await SubscriptionAsync(userId,tenantId,id,token);if(x is null)return new(404,"请求的资源不存在。");x.Name=r.Name??x.Name;x.Enabled=r.Enabled??x.Enabled;x.RefreshIntervalMin=r.RefreshIntervalMin??x.RefreshIntervalMin;await _db.SaveChangesAsync(token);return new(200,"更新成功。",new{id});}
    public async Task<ServiceResult> DeleteSubscriptionAsync(long userId,long tenantId,long id,CancellationToken token=default){var x=await SubscriptionAsync(userId,tenantId,id,token);if(x is null)return new(404,"请求的资源不存在。");x.DeletedAt=DateTime.UtcNow;await _db.SaveChangesAsync(token);return new(200,"删除成功。",new{id});}
    private Task<CalendarEvent?> EventAsync(long u,long t,long id,CancellationToken c)=>_db.CalendarEvents.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==u&&x.TenantId==t&&x.DeletedAt==null,c);
    private Task<CalendarSubscription?> SubscriptionAsync(long u,long t,long id,CancellationToken c)=>_db.CalendarSubscriptions.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==u&&x.TenantId==t&&x.DeletedAt==null,c);
    private static object EventView(CalendarEvent x)=>new{x.Id,x.Title,x.Description,x.Location,x.StartAt,x.EndAt,x.Timezone,x.AllDay,x.Color,x.Opacity,x.RepeatRule,x.CreatedAt,x.UpdatedAt};
}
