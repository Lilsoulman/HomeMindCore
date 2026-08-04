using HomeMind.Common.Model.ViewModel.Common;
using HomeMind.Common.Model.ViewModel.Data.Productivity;
namespace HomeMind.Business.IServices.Productivity;
public interface ICalendarServices
{
    Task<ServiceResult> ListEventsAsync(long userId,long tenantId,DateTime? from,DateTime? to,CancellationToken token=default);
    Task<ServiceResult> CreateEventAsync(long userId,long tenantId,CalendarEventRequest request,CancellationToken token=default);
    Task<ServiceResult> UpdateEventAsync(long userId,long tenantId,long id,CalendarEventRequest request,CancellationToken token=default);
    Task<ServiceResult> DeleteEventAsync(long userId,long tenantId,long id,CancellationToken token=default);
    Task<ServiceResult> ListSubscriptionsAsync(long userId,long tenantId,CancellationToken token=default);
    Task<ServiceResult> CreateSubscriptionAsync(long userId,long tenantId,SubscriptionRequest request,CancellationToken token=default);
    Task<ServiceResult> UpdateSubscriptionAsync(long userId,long tenantId,long id,SubscriptionRequest request,CancellationToken token=default);
    Task<ServiceResult> DeleteSubscriptionAsync(long userId,long tenantId,long id,CancellationToken token=default);
}
