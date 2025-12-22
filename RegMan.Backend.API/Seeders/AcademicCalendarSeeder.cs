using Microsoft.EntityFrameworkCore;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.API.Seeders;

public static class AcademicCalendarSeeder
{
    public static async Task EnsureDefaultRowAsync(AppDbContext context)
    {
        var exists = await context.AcademicCalendarSettings
            .AsNoTracking()
            .AnyAsync(s => s.SettingsKey == "default");

        if (exists)
            return;

        context.AcademicCalendarSettings.Add(new AcademicCalendarSettings
        {
            SettingsKey = "default",
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}
