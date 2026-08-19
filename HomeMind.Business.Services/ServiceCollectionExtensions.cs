using HomeMind.Business.IServices.Base;
using HomeMind.Business.Services.Base;
using HomeMind.Business.IServices.Media;
using HomeMind.Business.Services.Media;
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
using Microsoft.Extensions.Logging;
using HomeMind.Business.Services.Dashboard;
using HomeMind.Business.IServices.Family;
using HomeMind.Business.Services.Family;
using HomeMind.Business.IServices.Life;
using HomeMind.Business.Services.Life;
using HomeMind.Business.IServices.Finance;
using HomeMind.Business.Services.Finance;
using HomeMind.Business.IServices.Travel;
using HomeMind.Business.Services.Travel;
using HomeMind.Business.IServices.Steward;
using HomeMind.Business.Services.Steward;
using HomeMind.Business.IServices.Identity;
using HomeMind.Business.Services.Identity;
using HomeMind.Business.IServices.Conversation;
using HomeMind.Business.Services.Connectors;
using HomeMind.Business.Services.Conversation;
using HomeMind.Business.IServices.Memory;
using HomeMind.Business.Services.Memory;
using HomeMind.Business.Services.Connectors.Mcp;
using Microsoft.Extensions.Configuration;
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
        var homeAssistantMcp = new HomeAssistantMcpOptions
        {
            Mode = "rest_fallback",
            ServerName = "home-assistant",
            Enabled = false,
            Process = new McpProcessOptions()
        };
        services.AddSingleton<IMcpClientManager>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = config.GetSection("Mcp:Clients:HomeAssistant").Get<HomeAssistantMcpOptions>() ?? homeAssistantMcp;
            return new McpClientManager(_ => options.Enabled
                ? new StdioMcpClientSession(new StdioMcpProcessClient(options.Process))
                : new MockMcpClientSession());
        });
        services.AddScoped<HomeAssistantAdapter>();
        services.AddScoped<HomeAssistantMcpAdapter>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = config.GetSection("Mcp:Clients:HomeAssistant").Get<HomeAssistantMcpOptions>() ?? homeAssistantMcp;
            return new HomeAssistantMcpAdapter(sp.GetRequiredService<IMcpClientManager>(), sp.GetRequiredService<HomeMind.Common.Repository.HomeMindDbContext>(), options);
        });
        services.AddScoped<IDeviceAdapter>(sp => (IDeviceAdapter)SelectHomeAssistantAdapter(sp, homeAssistantMcp));
        services.AddScoped<IDeviceDiscovery>(sp => (IDeviceDiscovery)SelectHomeAssistantAdapter(sp, homeAssistantMcp));
        services.AddScoped<IDeviceCommandExecutor>(sp => (IDeviceCommandExecutor)SelectHomeAssistantAdapter(sp, homeAssistantMcp));
        services.AddScoped<DeviceSyncService>();
        services.AddScoped<IHomeAssistantEventSubscriber>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = config.GetSection("Mcp:Clients:HomeAssistant").Get<HomeAssistantMcpOptions>() ?? homeAssistantMcp;
            return new HomeAssistantEventSubscriber(sp.GetRequiredService<IConnectorSecretResolver>(), sp.GetRequiredService<DeviceSyncService>(), options);
        });
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
        services.AddScoped<IMindmapRunServices, MindmapRunServices>();
        services.AddScoped<IMemoryCandidateServices, MemoryCandidateServices>();
        services.AddScoped<ILearningMemoryServices, LearningMemoryServices>();
        services.AddScoped<IMemoryReviewServices, MemoryReviewServices>();
        services.AddScoped<IBillingServices, BillingServices>();
        // B29 快速剪辑素材登记：上传/路径登记 + ffprobe 元数据；素材仅本人可见可删。
        services.AddScoped<IFfprobeExtractor, FfprobeExtractor>();
        services.AddScoped<IClippingMaterialServices, ClippingMaterialServices>();
        // B38 素材自动发现：后台 Worker 扫描素材根目录登记新文件（白名单/时间窗/哈希去重/静默降级）。
        services.AddScoped<IClippingMaterialScanServices, ClippingMaterialScanServices>();
        // B32 剪辑对话引导：无状态 context 推进 + 规则意图匹配 + 模板回复；只引导不执行。
        services.AddScoped<IClippingChatServices, ClippingChatServices>();
        services.AddScoped<IClippingTaskServices, ClippingTaskServices>();
        services.AddScoped<IClippingRenderService, FfmpegRenderService>();
        services.AddScoped<IClippingPipelineServices, ClippingPipelineServices>();
        services.AddScoped<IClippingEngine>(sp => new ConfiguredClippingEngine("video_use", sp.GetRequiredService<IConfiguration>().GetSection("Clipping:Engines:VideoUse").Get<ClippingEngineOptions>() ?? new ClippingEngineOptions()));
        services.AddScoped<IClippingEngine>(sp => new ConfiguredClippingEngine("seedance", sp.GetRequiredService<IConfiguration>().GetSection("Clipping:Engines:Seedance").Get<ClippingEngineOptions>() ?? new ClippingEngineOptions()));
        services.AddScoped<IClippingEngine>(sp => new ConfiguredClippingEngine("hyperframes", sp.GetRequiredService<IConfiguration>().GetSection("Clipping:Engines:HyperFrames").Get<ClippingEngineOptions>() ?? new ClippingEngineOptions()));
        services.AddScoped<IClippingEngine>(sp => new ConfiguredClippingEngine("remotion", sp.GetRequiredService<IConfiguration>().GetSection("Clipping:Engines:Remotion").Get<ClippingEngineOptions>() ?? new ClippingEngineOptions()));
        // 剪映 MCP 客户端：默认 Mock（无本地 jianying-mcp 环境回退，测试用）；Mcp:Clients:Jianying:Enabled=true 时切换真实 stdio 实现。
        services.AddScoped<IClippingMcpClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (!config.GetValue<bool>("Mcp:Clients:Jianying:Enabled")) return new MockClippingMcpClient();
            var options = config.GetSection("Mcp:Clients:Jianying").Get<McpProcessOptions>() ?? new McpProcessOptions();
            return new JianyingMcpClient(new StdioMcpProcessClient(options));
        });
        services.AddScoped<IXhsConnectorServices, XhsConnectorServices>();
        services.AddScoped<IXhsPublishServices, XhsPublishServices>();
        // 本地 stdio MCP 进程客户端：进程级共享（单例），进程内懒启动；命令与超时来自配置。
        services.AddSingleton<IMcpProcessClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = config.GetSection("Mcp:Clients:Xhs").Get<McpProcessOptions>() ?? new McpProcessOptions();
            return new StdioMcpProcessClient(options);
        });
        // 小红书 MCP 客户端：默认 Mock（无本地 xhs-mcp 环境回退）；Mcp:Clients:Xhs:Enabled=true 时切换真实实现。
        services.AddScoped<IXhsMcpClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return config.GetValue<bool>("Mcp:Clients:Xhs:Enabled")
                ? new XhsMcpClient(sp.GetRequiredService<IMcpProcessClient>(), sp.GetRequiredService<ILogger<XhsMcpClient>>())
                : new MockXhsMcpClient();
        });
        return services;
    }

    /// <summary>按运行模式选择唯一的 Home Assistant 底层适配器，避免向业务层暴露双实现。</summary>
    private static object SelectHomeAssistantAdapter(IServiceProvider serviceProvider, HomeAssistantMcpOptions defaults)
    {
        var options = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Mcp:Clients:HomeAssistant").Get<HomeAssistantMcpOptions>() ?? defaults;
        return options.Mode.ToLowerInvariant() switch
        {
            "mcp" => serviceProvider.GetRequiredService<HomeAssistantMcpAdapter>(),
            "rest_fallback" => serviceProvider.GetRequiredService<HomeAssistantAdapter>(),
            "disabled" => throw new InvalidOperationException("Home Assistant 连接器已禁用。"),
            _ => throw new InvalidOperationException("Home Assistant MCP 运行模式无效。")
        };
    }
}
