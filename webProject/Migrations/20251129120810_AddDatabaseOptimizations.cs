using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webProject.Migrations
{
    public partial class AddDatabaseOptimizations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "IX_CalculationHistories_UserId_Success",
                table: "CalculationHistories",
                columns: new[] { "UserId", "Success" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CalculationHistory_MatrixSize",
                table: "CalculationHistories",
                sql: "\"Size\" > 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalculationHistories_UserId_Success",
                table: "CalculationHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CalculationHistory_MatrixSize",
                table: "CalculationHistories");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);
        }
    }
}
