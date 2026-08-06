using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// AI 配置启用开关（B18）：在 <c>ai_configs</c> 表新增 <c>enabled</c> 列，默认 <c>true</c>；
    /// 仅修改本表本列，遵循 Surgical Changes 原则，不触碰其他表与其他列。
    /// </summary>
    public partial class AddAiConfigEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enabled",
                table: "ai_configs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enabled",
                table: "ai_configs");
        }
    }
}
