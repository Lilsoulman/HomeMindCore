using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations;

/// <summary>V2.8 B35 剪辑任务持久化表；只建 clipping_tasks，不更新存量快照。</summary>
public partial class AddClippingTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "clipping_tasks",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                run_id = table.Column<long>(type: "bigint", nullable: true),
                status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "collecting"),
                materials_json = table.Column<string>(type: "json", nullable: false),
                goal = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                current_plan_json = table.Column<string>(type: "json", nullable: true),
                version_history_json = table.Column<string>(type: "json", nullable: false),
                engine_stage = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                created_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                deleted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_clipping_tasks", x => x.id);
                table.ForeignKey("FK_clipping_tasks_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                table.ForeignKey("FK_clipping_tasks_runs_run_id", x => x.run_id, "expert_runs", "id");
            });
        migrationBuilder.CreateIndex(name: "idx_clipping_tasks_owner", table: "clipping_tasks", columns: new[] { "tenant_id", "created_by_user_id", "deleted_at" });
        migrationBuilder.CreateIndex(name: "idx_clipping_tasks_run", table: "clipping_tasks", column: "run_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "clipping_tasks");
}
