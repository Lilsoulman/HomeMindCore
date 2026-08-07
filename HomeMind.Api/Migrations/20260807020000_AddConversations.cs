using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B20 专家会话（V2.4，第六阶段）：新增 <c>conversations</c>（专家对话框，个人资源）
    /// 与 <c>conversation_messages</c>（user/assistant 消息，run_id 追溯）两张表，扩展
    /// <c>expert_runs.conversation_id</c> 可空列关联会话。
    /// 同步 SQL 见 <c>database/026_expert_conversations.mysql.sql</c>；仅修改本切片相关表，
    /// 遵循 Surgical Changes 原则，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    expert_id = table.Column<long>(type: "bigint", nullable: true),
                    expert_version_id = table.Column<long>(type: "bigint", nullable: true),
                    workspace_connector_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.ForeignKey("FK_conversations_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                    table.ForeignKey("FK_conversations_users_owner_user_id", x => x.owner_user_id, "users", "id");
                    table.ForeignKey("FK_conversations_experts_expert_id", x => x.expert_id, "experts", "id");
                    table.ForeignKey("FK_conversations_expert_versions_expert_version_id", x => x.expert_version_id, "expert_versions", "id");
                    table.ForeignKey("FK_conversations_workspace_connectors_workspace_connector_id", x => x.workspace_connector_id, "workspace_connectors", "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_tenant_id_owner_user_id_deleted_at_updated_at",
                table: "conversations",
                columns: new[] { "tenant_id", "owner_user_id", "deleted_at", "updated_at" });

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    run_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_messages", x => x.id);
                    table.ForeignKey("FK_conversation_messages_conversations_conversation_id", x => x.conversation_id, "conversations", "id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_conversation_messages_expert_runs_run_id", x => x.run_id, "expert_runs", "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_messages_conversation_id_run_id",
                table: "conversation_messages",
                columns: new[] { "conversation_id", "run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversation_messages_conversation_id_id",
                table: "conversation_messages",
                columns: new[] { "conversation_id", "id" });

            migrationBuilder.AddColumn<long>(
                name: "conversation_id",
                table: "expert_runs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_expert_runs_conversation_id_id",
                table: "expert_runs",
                columns: new[] { "conversation_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "FK_expert_runs_conversations_conversation_id",
                table: "expert_runs",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expert_runs_conversations_conversation_id",
                table: "expert_runs");

            migrationBuilder.DropIndex(
                name: "IX_expert_runs_conversation_id_id",
                table: "expert_runs");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                table: "expert_runs");

            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
