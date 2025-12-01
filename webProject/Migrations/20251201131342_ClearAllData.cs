﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webProject.Migrations
{
    /// <inheritdoc />
    public partial class ClearAllData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all calculation histories first (foreign key constraint)
            migrationBuilder.Sql("DELETE FROM \"CalculationHistories\"");
            
            // Delete all users
            migrationBuilder.Sql("DELETE FROM \"Users\"");
            
            // Reset auto-increment sequences in PostgreSQL
            migrationBuilder.Sql("ALTER SEQUENCE \"CalculationHistories_Id_seq\" RESTART WITH 1");
            migrationBuilder.Sql("ALTER SEQUENCE \"Users_Id_seq\" RESTART WITH 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore deleted data
            // This migration is irreversible
        }
    }
}
