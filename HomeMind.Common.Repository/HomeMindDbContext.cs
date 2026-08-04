using HomeMind.Common.Model.Entities;
using HomeMind.Common.Model.Entities.SmartHome;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Common.Repository;

/// <summary>HomeMind 数据库上下文，集中定义所有物理表与实体的对应关系。</summary>
public sealed class HomeMindDbContext : DbContext
{
    public HomeMindDbContext(DbContextOptions<HomeMindDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
    public DbSet<AuthDevice> AuthDevices => Set<AuthDevice>();
    public DbSet<AuthRefreshToken> AuthRefreshTokens => Set<AuthRefreshToken>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<AccessTokenRevocation> AccessTokenRevocations => Set<AccessTokenRevocation>();
    public DbSet<AuthVerificationChallenge> AuthVerificationChallenges => Set<AuthVerificationChallenge>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();
    public DbSet<SyncClient> SyncClients => Set<SyncClient>();
    public DbSet<SyncMutation> SyncMutations => Set<SyncMutation>();
    public DbSet<SyncChangeLog> SyncChangeLogs => Set<SyncChangeLog>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoTag> TodoTags => Set<TodoTag>();
    public DbSet<TodoTagLink> TodoTagLinks => Set<TodoTagLink>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<CalendarEventException> CalendarEventExceptions => Set<CalendarEventException>();
    public DbSet<IcalOverride> IcalOverrides => Set<IcalOverride>();
    public DbSet<AiSkill> AiSkills => Set<AiSkill>();
    public DbSet<AiConfig> AiConfigs => Set<AiConfig>();
    public DbSet<AiCallLog> AiCallLogs => Set<AiCallLog>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanItem> PlanItems => Set<PlanItem>();
    public DbSet<Expert> Experts => Set<Expert>();
    public DbSet<ExpertVersion> ExpertVersions => Set<ExpertVersion>();
    public DbSet<ExpertGroup> ExpertGroups => Set<ExpertGroup>();
    public DbSet<ExpertGroupVersion> ExpertGroupVersions => Set<ExpertGroupVersion>();
    public DbSet<ExpertGroupMember> ExpertGroupMembers => Set<ExpertGroupMember>();
    public DbSet<UserExpertPreference> UserExpertPreferences => Set<UserExpertPreference>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<ExpertRunContext> ExpertRunContexts => Set<ExpertRunContext>();
    public DbSet<RunStep> RunSteps => Set<RunStep>();
    public DbSet<RunStepDependency> RunStepDependencies => Set<RunStepDependency>();
    public DbSet<ExpertJob> ExpertJobs => Set<ExpertJob>();
    public DbSet<RunEvent> RunEvents => Set<RunEvent>();
    public DbSet<RunArtifact> RunArtifacts => Set<RunArtifact>();
    public DbSet<RunStepUsage> RunStepUsages => Set<RunStepUsage>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<ExpertRunAction> ExpertRunActions => Set<ExpertRunAction>();
    public DbSet<ActionExecutionAudit> ActionExecutionAudits => Set<ActionExecutionAudit>();
    public DbSet<ConnectorProvider> ConnectorProviders => Set<ConnectorProvider>();
    public DbSet<WorkspaceConnector> WorkspaceConnectors => Set<WorkspaceConnector>();
    public DbSet<UserConnectorAuthorization> UserConnectorAuthorizations => Set<UserConnectorAuthorization>();
    public DbSet<SmartHomeSpace> SmartHomeSpaces => Set<SmartHomeSpace>();
    public DbSet<SmartHomeDevice> SmartHomeDevices => Set<SmartHomeDevice>();
    public DbSet<DeviceCapability> DeviceCapabilities => Set<DeviceCapability>();
    public DbSet<DeviceState> DeviceStates => Set<DeviceState>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<SceneAction> SceneActions => Set<SceneAction>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<ConnectorSyncJob> ConnectorSyncJobs => Set<ConnectorSyncJob>();
    public DbSet<ExpertFile> ExpertFiles => Set<ExpertFile>();
    public DbSet<ExpertFileObject> ExpertFileObjects => Set<ExpertFileObject>();
    public DbSet<ExpertFileAttachment> ExpertFileAttachments => Set<ExpertFileAttachment>();
    public DbSet<TeamRunTemplate> TeamRunTemplates => Set<TeamRunTemplate>();
    public DbSet<TeamRunTemplateVersion> TeamRunTemplateVersions => Set<TeamRunTemplateVersion>();
    public DbSet<TeamRun> TeamRuns => Set<TeamRun>();
    public DbSet<TeamRunMember> TeamRunMembers => Set<TeamRunMember>();
    public DbSet<TeamRunAudit> TeamRunAudits => Set<TeamRunAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TenantMember>().HasKey(x => new { x.TenantId, x.UserId });
        modelBuilder.Entity<SyncMutation>().HasKey(x => new { x.ClientId, x.MutationId });
        modelBuilder.Entity<TodoTagLink>().HasKey(x => new { x.TodoId, x.TagId });
        modelBuilder.Entity<UserSetting>().HasKey(x => new { x.UserId, x.Key });
        modelBuilder.Entity<UserConsent>().HasKey(x => new { x.UserId, x.ConsentType, x.Version });
        modelBuilder.Entity<ExpertGroupMember>().HasKey(x => new { x.GroupVersionId, x.ExpertVersionId });
        modelBuilder.Entity<UserExpertPreference>().HasKey(x => new { x.TenantId, x.UserId, x.ExpertId });
        modelBuilder.Entity<RunStepDependency>().HasKey(x => new { x.StepId, x.DependsOnStepId });

        ConfigureStoreGeneratedTimestamps(modelBuilder);

        // MySQL JSON 字段按字符串承载，避免将原始 JSON 再次序列化为字符串。
        modelBuilder.Entity<AiSkill>().Property(x => x.Scopes).HasColumnType("json");
        modelBuilder.Entity<Expert>().Property(x => x.PrivacyScope).HasColumnType("json");
        modelBuilder.Entity<ExpertVersion>().Property(x => x.ToolPolicy).HasColumnType("json");
        modelBuilder.Entity<ExpertVersion>().Property(x => x.OutputSchema).HasColumnType("json");
        modelBuilder.Entity<ExpertGroupVersion>().Property(x => x.OrchestrationPolicy).HasColumnType("json");
        modelBuilder.Entity<ExpertGroupVersion>().Property(x => x.OutputSchema).HasColumnType("json");
        modelBuilder.Entity<AgentRun>().Property(x => x.Input).HasColumnType("json");
        modelBuilder.Entity<AgentRun>().Property(x => x.Result).HasColumnType("json");
        modelBuilder.Entity<RunEvent>().Property(x => x.Payload).HasColumnType("json");
        modelBuilder.Entity<ExpertRunAction>().Property(x => x.RequestJson).HasColumnType("json");
        modelBuilder.Entity<ExpertRunAction>().Property(x => x.Result).HasColumnType("json");
        modelBuilder.Entity<ActionExecutionAudit>().Property(x => x.Command).HasColumnType("json");
        modelBuilder.Entity<ActionExecutionAudit>().Property(x => x.Result).HasColumnType("json");
        modelBuilder.Entity<UserConnectorAuthorization>().Property(x => x.Scope).HasColumnType("json");
        modelBuilder.Entity<DeviceCapability>().Property(x => x.ValueSchema).HasColumnType("json");
        modelBuilder.Entity<DeviceState>().Property(x => x.State).HasColumnType("json");
        modelBuilder.Entity<SceneAction>().Property(x => x.TargetValue).HasColumnType("json");
        modelBuilder.Entity<AutomationRule>().Property(x => x.TriggerConfig).HasColumnType("json");
        modelBuilder.Entity<AutomationRule>().Property(x => x.Conditions).HasColumnType("json");
        modelBuilder.Entity<AutomationRule>().Property(x => x.Actions).HasColumnType("json");
        modelBuilder.Entity<TeamRunTemplate>().Property(x => x.GraphJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunTemplateVersion>().Property(x => x.MembersJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunTemplateVersion>().Property(x => x.FileRefsJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunTemplateVersion>().Property(x => x.PermissionIntersectionsJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunTemplateVersion>().Property(x => x.GraphJson).HasColumnType("json");
        modelBuilder.Entity<TeamRun>().Property(x => x.SynthesisResultJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunMember>().Property(x => x.PermissionIntersectionJson).HasColumnType("json");
        modelBuilder.Entity<TeamRunAudit>().Property(x => x.PayloadJson).HasColumnType("json");
    }

    private static void ConfigureStoreGeneratedTimestamps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<User>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<UserIdentity>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<PasswordCredential>().Property(x => x.PasswordChangedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<AuthDevice>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<AuthRefreshToken>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<Tenant>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<Tenant>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<TenantMember>().Property(x => x.JoinedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<TenantMember>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<TenantMember>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<Todo>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<Todo>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<Subtask>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<Subtask>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<CalendarEvent>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<CalendarEvent>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<CalendarSubscription>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<CalendarSubscription>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<AiSkill>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<AiSkill>().Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        modelBuilder.Entity<AgentRun>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        modelBuilder.Entity<RunEvent>().Property(x => x.CreatedAt).ValueGeneratedOnAdd();
    }
}
