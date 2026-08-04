using HomeMind.Business.IServices.Base;
using HomeMind.Business.Services.Base;
using HomeMind.Business.IServices.Productivity;
using HomeMind.Business.Services.Productivity;
using HomeMind.Business.IServices.AI;
using HomeMind.Business.Services.AI;
using HomeMind.Business.IServices.Agent;
using HomeMind.Business.Services.Agent;
using HomeMind.Business.IServices.Expert;
using HomeMind.Business.Services.Expert;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Business.IServices.Dashboard;
using HomeMind.Business.Services.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace HomeMind.Business.Services;

/// <summary>注册业务服务实现。</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHomeMindBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IBaseUserServices, BaseUserServices>();
        services.AddScoped<ITodoServices, TodoServices>();
        services.AddScoped<ICalendarServices, CalendarServices>();
        services.AddScoped<IAiSkillServices, AiSkillServices>();
        services.AddScoped<IAgentRunServices, AgentRunServices>();
        services.AddScoped<IExpertCatalogServices, ExpertCatalogServices>();
        services.AddScoped<IHousekeeperRunServices, HousekeeperRunServices>();
        services.AddScoped<ISmartHomeReadServices, SmartHomeReadServices>();
        services.AddScoped<ISmartHomeSceneServices, SmartHomeSceneServices>();
        services.AddScoped<IDashboardServices, DashboardServices>();
        services.AddScoped<IConnectorServices, ConnectorServices>();
        services.AddScoped<IAutomationRuleServices, AutomationRuleServices>();
        services.AddScoped<IConnectorRuntimeServices, ConnectorRuntimeServices>();
        services.AddSingleton<IConnectorSyncQueue, ChannelConnectorSyncQueue>();
        services.AddSingleton<IConnectorSecretReferenceValidator, ConfigurationConnectorSecretReferenceValidator>();
        services.AddSingleton<IConnectorSecretResolver, HashiCorpVaultConnectorSecretResolver>();
        services.AddScoped<IConnectorAdapter, HomeAssistantConnectorAdapter>();
        services.AddScoped<IExpertFileServices, ExpertFileServices>();
        services.AddScoped<ITeamRunServices, TeamRunServices>();
        services.AddSingleton<IExpertFileStorage, LocalExpertFileStorage>();
        services.AddSingleton<IExpertFileScanner, LocalExpertFileScanner>();
        return services;
    }
}
