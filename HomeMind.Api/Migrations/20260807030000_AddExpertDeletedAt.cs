using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B21 自建专家（V2.4，第六阶段）：为 <c>experts</c> 增加 <c>deleted_at</c> 软删除列，
    /// 支撑用户自建专家的软删除（owner_user_id / created_at / updated_at / row_version 自 002 已有，
    /// 实体映射在 B21 补齐）。
    /// 同步 SQL 见 <c>database/027_expert_self_serve.mysql.sql</c>；仅修改本切片相关表，
    /// 遵循 Surgical Changes 原则，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddExpertDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "experts",
                type: "datetime(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "experts");
        }
    }
}
