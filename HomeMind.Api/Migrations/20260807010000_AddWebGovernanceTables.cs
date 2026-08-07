using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B19 Web 治理 API（V2.4）：新增 <c>tenant_member_invitations</c>（手机号 SHA-256 邀请）
    /// 与 <c>web_navigation_preferences</c>（角色粒度 Web 导航偏好）两张表，扩展
    /// <c>family_audit_logs</c> 的 action / target_type CHECK 各加 7 个 / 3 个新值。
    /// 同步 SQL 见 <c>database/025_v2.4_web_governance.mysql.sql</c>；仅修改本切片相关表，
    /// 遵循 Surgical Changes 原则，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddWebGovernanceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_member_invitations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    invited_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    subject_kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "phone"),
                    subject_hash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    proposed_role = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    expires_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    accepted_user_id = table.Column<long>(type: "bigint", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_member_invitations", x => x.id);
                    table.ForeignKey("FK_tenant_member_invitations_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                    table.ForeignKey("FK_tenant_member_invitations_users_invited_by_user_id", x => x.invited_by_user_id, "users", "id");
                    table.ForeignKey("FK_tenant_member_invitations_users_accepted_user_id", x => x.accepted_user_id, "users", "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_member_invitations_tenant_id_subject_hash",
                table: "tenant_member_invitations",
                columns: new[] { "tenant_id", "subject_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_member_invitations_tenant_id_status_expires_at",
                table: "tenant_member_invitations",
                columns: new[] { "tenant_id", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_member_invitations_subject_kind_subject_hash",
                table: "tenant_member_invitations",
                columns: new[] { "subject_kind", "subject_hash" });

            migrationBuilder.CreateTable(
                name: "web_navigation_preferences",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    route_key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    updated_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_web_navigation_preferences", x => x.id);
                    table.ForeignKey("FK_web_navigation_preferences_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                    table.ForeignKey("FK_web_navigation_preferences_users_updated_by_user_id", x => x.updated_by_user_id, "users", "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_web_navigation_preferences_tenant_id_role_route_key",
                table: "web_navigation_preferences",
                columns: new[] { "tenant_id", "role", "route_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_web_navigation_preferences_tenant_id_role_sort_order",
                table: "web_navigation_preferences",
                columns: new[] { "tenant_id", "role", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "web_navigation_preferences");

            migrationBuilder.DropTable(
                name: "tenant_member_invitations");
        }
    }
}
