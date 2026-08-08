using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B24 快速剪辑 Skill 基线（V2.5）：新增 <c>skills</c> 平台级 Skill 目录表
    /// （tenant_id 固定 1，同 <c>scenario_templates</c> 惯例），种子 quick-edit 由
    /// <c>database/029_quick_edit_skill.mysql.sql</c> 写入。仅建表、无 CHECK、
    /// 不更新快照，遵循 Surgical Changes 约定，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddSkillCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    input_schema_json = table.Column<string>(type: "json", nullable: false),
                    output_schema_json = table.Column<string>(type: "json", nullable: true),
                    required_permission = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    risk_level = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, defaultValue: "L1"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(3)", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    sync_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                    table.ForeignKey("FK_skills_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                });

            migrationBuilder.CreateIndex(
                name: "uk_skills_key",
                table: "skills",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
