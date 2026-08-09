using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B29 快速剪辑素材登记（V2.7 对话式优化）：新增 <c>clipping_materials</c> 素材登记表
    /// （浏览器上传落盘或路径模式登记的输入文件，ffprobe 提取时长/分辨率/帧率元数据）。
    /// 仅建表、不扩展 CHECK、不更新快照，遵循 Surgical Changes 约定，不触碰既有 schema 漂移。
    /// </summary>
    public partial class AddClippingMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clipping_materials",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    storage_path = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
                    content_type = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    duration_seconds = table.Column<int>(type: "int", nullable: true),
                    width = table.Column<int>(type: "int", nullable: true),
                    height = table.Column<int>(type: "int", nullable: true),
                    fps = table.Column<double>(type: "double", nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime(3)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clipping_materials", x => x.id);
                    table.ForeignKey("FK_clipping_materials_tenants_tenant_id", x => x.tenant_id, "tenants", "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_clipping_materials_owner",
                table: "clipping_materials",
                columns: new[] { "owner_user_id", "is_deleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clipping_materials");
        }
    }
}
