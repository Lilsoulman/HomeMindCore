using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMind.Api.Migrations
{
    /// <summary>
    /// 将 10 个 char(36) 列改为 varchar(36)。
    /// MySqlConnector 2.3+ 的 GuidFormat.Default 会把 char(36) 列中符合 GUID 格式的值按 Guid 读取，
    /// 与实体 string 属性不匹配导致 InvalidCastException（登录/刷新令牌 500）；varchar(36) 不受影响。
    /// </summary>
    public partial class FixChar36ToVarchar36 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "installation_id",
                table: "auth_devices",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "family_id",
                table: "auth_refresh_tokens",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                table: "auth_verification_challenges",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "connector_sync_jobs",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "credit_ledger",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "expert_jobs",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "request_idempotency_key",
                table: "expert_run_actions",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "request_idempotency_key",
                table: "expert_runs",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "installation_id",
                table: "sync_clients",
                type: "varchar(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "mutation_id",
                table: "sync_mutations",
                type: "varchar(36)",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "installation_id",
                table: "auth_devices",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "family_id",
                table: "auth_refresh_tokens",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "id",
                table: "auth_verification_challenges",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "connector_sync_jobs",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "credit_ledger",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "expert_jobs",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "request_idempotency_key",
                table: "expert_run_actions",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "request_idempotency_key",
                table: "expert_runs",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "installation_id",
                table: "sync_clients",
                type: "char(36)",
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "mutation_id",
                table: "sync_mutations",
                type: "char(36)",
                nullable: false);
        }
    }
}
