using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App01.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PortalFirstUserAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            INSERT INTO [Portal].[Users] ([Email], [PasswordHash], [IsAdmin], [CreatedAt]) 
            VALUES
            ('tomsoft1@poczta.fm', '$2a$10$qjkh5mQmS7UYTVMTB/yArui1/zgK.7deh6.UOAcz5A5Hps7.5JAYy', 1, GETDATE())
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            DELETE FROM [Portal].[Users];
            ");
        }
    }
}
