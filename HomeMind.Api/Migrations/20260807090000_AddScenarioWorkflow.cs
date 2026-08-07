using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B22 场景工作流（第七阶段）：新增 <c>scenario_templates</c>（平台级场景模板）
    /// 与 <c>scenario_instances</c>（家庭启用实例，Device Resolver 解析后的步骤）两张表。
    /// 同步 SQL 见 <c>database/028_scenario_workflow.mysql.sql</c>；仅建表、无 CHECK、
    /// 不更新快照，遵循 Surgical Changes 约定，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddScenarioWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scenario_templates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    trigger_keywords_json = table.Column<string>(type: "json", nullable: true),
                    steps_json = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    sync_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_templates", x => x.id);
                    table.ForeignKey("FK_scenario_templates_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                });

            migrationBuilder.CreateIndex(
                name: "uk_scenario_templates_code",
                table: "scenario_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateTable(
                name: "scenario_instances",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    template_code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    trigger_keywords_json = table.Column<string>(type: "json", nullable: true),
                    steps_json = table.Column<string>(type: "json", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "enabled"),
                    created_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    sync_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_instances", x => x.id);
                    table.ForeignKey("FK_scenario_instances_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_scenario_instances_tenant",
                table: "scenario_instances",
                columns: new[] { "tenant_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenario_instances");

            migrationBuilder.DropTable(
                name: "scenario_templates");
        }
    }
}
