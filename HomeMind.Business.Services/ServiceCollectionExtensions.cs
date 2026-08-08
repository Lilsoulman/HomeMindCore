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
using HomeMind.Business.IServices.Connector;
using HomeMind.Business.IServices.SmartHome;
using HomeMind.Business.Services.Connectors.Adapters;
using HomeMind.Business.Services.Connectors.Bridge;
using HomeMind.Business.Services.SmartHome;
using HomeMind.Business.IServices.Dashboard;
using HomeMind.Business.Services.Dashboard;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Business.IServices.Life;
using HomeMind.Business.Services.Life;
using HomeMind.Business.IServices.Travel;
using HomeMind.Business.Services.Travel;
using HomeMind.Business.IServices.Steward;
using HomeMind.Business.Services.Steward;
using HomeMind.Business.IServices.Identity;
using HomeMind.Business.Services.Identity;
using HomeMind.Business.IServices.Conversation;
using HomeMind.Business.Services.Conversation;
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
        services.AddScoped<IKnowledgeItemServices, KnowledgeItemServices>();
        services.AddScoped<IAiConfigServices, AiConfigServices>();
        services.AddScoped<ILLMClient, OpenAICompatibleLlmClient>();
        services.AddScoped<IAgentRunServices, AgentRunServices>();
        services.AddScoped<IAgentRunProcessor, AgentRunProcessor>();
        services.AddScoped<IExpertCatalogServices, ExpertCatalogServices>();
        services.AddScoped<IHousekeeperRunServices, HousekeeperRunServices>();
        services.AddScoped<ISmartHomeReadServices, SmartHomeReadServices>();
        services.AddScoped<ISmartHomeSceneServices, SmartHomeSceneServices>();
        services.AddScoped<IScenarioWorkflowServices, ScenarioWorkflowServices>();
        services.AddScoped<IDashboardServices, DashboardServices>();
        services.AddScoped<IConnectorServices, ConnectorServices>();
        services.AddScoped<IConnectorAuthorizationServices, ConnectorAuthorizationServices>();
        services.AddScoped<IAutomationRuleServices, AutomationRuleServices>();
        services.AddScoped<IConnectorRuntimeServices, ConnectorRuntimeServices>();
        services.AddSingleton<IConnectorSyncQueue, ChannelConnectorSyncQueue>();
        services.AddSingleton<IConnectorSecretReferenceValidator, ConfigurationConnectorSecretReferenceValidator>();
        services.AddSingleton<IConnectorSecretResolver, HashiCorpVaultConnectorSecretResolver>();
        services.AddScoped<IDeviceAdapter, HomeAssistantAdapter>();
        services.AddScoped<IDeviceDiscovery, HomeAssistantAdapter>();
        services.AddScoped<IDeviceCommandExecutor, HomeAssistantAdapter>();
        services.AddScoped<DeviceSyncService>();
        services.AddScoped<CommandRelayService>();
        services.AddScoped<IExpertFileServices, ExpertFileServices>();
        services.AddScoped<IPptxBuilder, OpenXmlPptxBuilder>();
        services.AddScoped<ITeamRunServices, TeamRunServices>();
        services.AddSingleton<IExpertFileStorage, LocalExpertFileStorage>();
        services.AddSingleton<IExpertFileScanner, LocalExpertFileScanner>();
        services.AddScoped<IFamilyAuditLogger, FamilyAuditLogger>();
        services.AddScoped<IFamilyMemberServices, FamilyMemberServices>();
        services.AddScoped<IFamilyKnowledgeServices, FamilyKnowledgeServices>();
        services.AddScoped<IFamilyDecisionServices, FamilyDecisionServices>();
        services.AddScoped<IStewardServices, StewardServices>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<ITravelRecommendationServices, TravelRecommendationServices>();
        services.AddScoped<ILifeExpertRunServices, LifeExpertRunServices>();
        services.AddScoped<ITenantMemberServices, TenantMemberServices>();
        services.AddScoped<ITenantMemberInvitationServices, TenantMemberInvitationServices>();
        services.AddScoped<IWebNavigationPreferencesServices, WebNavigationPreferencesServices>();
        services.AddScoped<IConversationServices, ConversationServices>();
        services.AddScoped<IExpertSelfServeServices, ExpertSelfServeServices>();
        services.AddScoped<ISkillRunServices, SkillRunServices>();
        services.AddScoped<IClippingMcpClient, MockClippingMcpClient>();
        return services;
    }
}
