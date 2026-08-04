using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeMind.Common.Model.Entities;

// 以下实体为已建表但暂未开放业务接口的数据表。保留独立实体和表名映射，新增接口时不得绕过服务层直接编写 SQL。
[Table("auth_verification_challenges")] public sealed class AuthVerificationChallenge { [Key, Column("id")] public string Id { get; set; } = null!; }
[Table("auth_audit_logs")] public sealed class AuthAuditLog { [Key, Column("id")] public long Id { get; set; } }
[Table("sync_clients")] public sealed class SyncClient { [Key, Column("id")] public long Id { get; set; } }
[Table("sync_mutations")] public sealed class SyncMutation { [Column("client_id")] public long ClientId { get; set; } [Column("mutation_id")] public string MutationId { get; set; } = null!; }
[Table("sync_change_log")] public sealed class SyncChangeLog { [Key, Column("sync_version")] public long SyncVersion { get; set; } }
[Table("todo_lists")] public sealed class TodoList { [Key, Column("id")] public long Id { get; set; } }
[Table("todo_tags")] public sealed class TodoTag { [Key, Column("id")] public long Id { get; set; } }
[Table("todo_tag_links")] public sealed class TodoTagLink { [Column("todo_id")] public long TodoId { get; set; } [Column("tag_id")] public long TagId { get; set; } }
[Table("attachments")] public sealed class Attachment { [Key, Column("id")] public long Id { get; set; } }
[Table("calendar_event_exceptions")] public sealed class CalendarEventException { [Key, Column("id")] public long Id { get; set; } }
[Table("ical_overrides")] public sealed class IcalOverride { [Key, Column("id")] public long Id { get; set; } }
[Table("ai_configs")] public sealed class AiConfig { [Key, Column("user_id")] public long UserId { get; set; } }
[Table("ai_call_logs")] public sealed class AiCallLog { [Key, Column("id")] public long Id { get; set; } }
[Table("user_settings")] public sealed class UserSetting { [Column("user_id")] public long UserId { get; set; } [Column("k")] public string Key { get; set; } = null!; }
[Table("push_subscriptions")] public sealed class PushSubscription { [Key, Column("id")] public long Id { get; set; } }
[Table("user_consents")] public sealed class UserConsent { [Column("user_id")] public long UserId { get; set; } [Column("consent_type")] public string ConsentType { get; set; } = null!; [Column("version")] public string Version { get; set; } = null!; }
[Table("plans")] public sealed class Plan { [Key, Column("id")] public long Id { get; set; } }
[Table("plan_items")] public sealed class PlanItem { [Key, Column("id")] public long Id { get; set; } }
[Table("expert_group_members")] public sealed class ExpertGroupMember { [Column("group_version_id")] public long GroupVersionId { get; set; } [Column("expert_version_id")] public long ExpertVersionId { get; set; } }
[Table("user_expert_preferences")] public sealed class UserExpertPreference { [Column("tenant_id")] public long TenantId { get; set; } [Column("user_id")] public long UserId { get; set; } [Column("expert_id")] public long ExpertId { get; set; } }
[Table("expert_run_contexts")] public sealed class ExpertRunContext { [Key, Column("id")] public long Id { get; set; } }
[Table("run_steps")] public sealed class RunStep { [Key, Column("id")] public long Id { get; set; } }
[Table("run_step_dependencies")] public sealed class RunStepDependency { [Column("step_id")] public long StepId { get; set; } [Column("depends_on_step_id")] public long DependsOnStepId { get; set; } }
[Table("run_artifacts")] public sealed class RunArtifact { [Key, Column("id")] public long Id { get; set; } }
[Table("run_step_usage")] public sealed class RunStepUsage { [Key, Column("id")] public long Id { get; set; } }
[Table("credit_ledger")] public sealed class CreditLedgerEntry { [Key, Column("id")] public long Id { get; set; } }
