﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace webProject.Migrations
{
    public partial class ClearAllData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"CalculationHistories\"");
            
            migrationBuilder.Sql("DELETE FROM \"Users\"");
            
            migrationBuilder.Sql("ALTER SEQUENCE \"CalculationHistories_Id_seq\" RESTART WITH 1");
            migrationBuilder.Sql("ALTER SEQUENCE \"Users_Id_seq\" RESTART WITH 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
