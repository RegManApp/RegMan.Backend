using Microsoft.AspNetCore.Identity;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.API.Seeders
{
    public static class UserSeeder
    {
        public static async Task SeedAdminAsync(UserManager<BaseUser> userManager)
        {
            string? email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            string? password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return;
            }

            var admin = await userManager.FindByEmailAsync(email);

            if (admin == null)
            {
                admin = new BaseUser
                {
                    UserName = email,
                    Email = email,
                    FullName = "System Admin",
                    Address = "HQ",
                    Role = "Admin"
                };

                await userManager.CreateAsync(admin, password);
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}