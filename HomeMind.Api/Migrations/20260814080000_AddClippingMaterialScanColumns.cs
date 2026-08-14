using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// B38 素材自动发现（V2.9 剪辑体验重构）：<c>clipping_materials</c> 加
    /// <c>source_type</c>（upload/scan）与 <c>directory_key</c>（路径 SHA-256 去重键）两列，
    /// 并建立唯一索引 <c>uk_clipping_materials_directory_key</c> 兜底并发去重。
    /// 仅加列与索引、不扩展 CHECK、不更新快照，遵循 Surgical Changes 约定，不触碰既有 schema 漂移
    /// （真实 MySQL 结构以 database/041_clipping_material_scan.mysql.sql 为准，本迁移仅服务 EF 工具链）。
    /// </summary>
    public partial class AddClippingMaterialScanColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "clipping_materials",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "upload");

            migrationBuilder.AddColumn<string>(
                name: "directory_key",
                table: "clipping_materials",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uk_clipping_materials_directory_key",
                table: "clipping_materials",
                column: "directory_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uk_clipping_materials_directory_key",
                table: "clipping_materials");

            migrationBuilder.DropColumn(
                name: "directory_key",
                table: "clipping_materials");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "clipping_materials");
        }
    }
}
