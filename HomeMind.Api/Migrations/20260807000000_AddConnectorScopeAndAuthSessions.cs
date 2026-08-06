using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B18 Connector Scope 与 OAuth 基线（V2.4）：<c>workspace_connectors</c> 新增
    /// <c>binding_scope</c>/<c>owner_user_id</c>/<c>auth_status</c>/<c>config</c> 四列，
    /// 新增 <c>connector_authorization_sessions</c> 短期授权会话表，<c>expert_runs</c>
    /// 新增 <c>permission_snapshot_json</c> 权限快照列；仅修改本切片相关表，遵循
    /// Surgical Changes 原则，不触碰既有 schema 漂移。CHECK 约束与 Provider 目录
    /// 注册见 <c>database/024_v2.4_connector_scope.mysql.sql</c>。
    /// </summary>
    public partial class AddConnectorScopeAndAuthSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "binding_scope",
                table: "workspace_connectors",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "household");

            migrationBuilder.AddColumn<long>(
                name: "owner_user_id",
                table: "workspace_connectors",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "auth_status",
                table: "workspace_connectors",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "config",
                table: "workspace_connectors",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "permission_snapshot_json",
                table: "expert_runs",
                type: "json",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "connector_authorization_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    connector_provider_id = table.Column<long>(type: "bigint", nullable: false),
                    binding_scope = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "personal"),
                    initiator_user_id = table.Column<long>(type: "bigint", nullable: false),
                    state_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    pkce_verifier_ref = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    redirect_uri = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    expires_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connector_authorization_sessions", x => x.id);
                    table.ForeignKey("FK_connector_authorization_sessions_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                    table.ForeignKey("FK_connector_authorization_sessions_connector_providers_connector_provider_id", x => x.connector_provider_id, "connector_providers", "id");
                    table.ForeignKey("FK_connector_authorization_sessions_users_initiator_user_id", x => x.initiator_user_id, "users", "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_connector_authorization_sessions_tenant_id_status",
                table: "connector_authorization_sessions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_connector_authorization_sessions_initiator_user_id",
                table: "connector_authorization_sessions",
                columns: new[] { "initiator_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connector_authorization_sessions");

            migrationBuilder.DropColumn(
                name: "binding_scope",
                table: "workspace_connectors");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "workspace_connectors");

            migrationBuilder.DropColumn(
                name: "auth_status",
                table: "workspace_connectors");

            migrationBuilder.DropColumn(
                name: "config",
                table: "workspace_connectors");

            migrationBuilder.DropColumn(
                name: "permission_snapshot_json",
                table: "expert_runs");
        }
    }
}
