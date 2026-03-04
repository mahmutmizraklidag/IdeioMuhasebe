using IdeioMuhasebe.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdeioMuhasebe.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeedAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            await db.Database.MigrateAsync();

            // 1) admin kullanıcı yoksa ekle
            if (!await db.Users.AnyAsync())
            {
                db.Users.Add(new User { Username = "admin", password = "123456" });
                await db.SaveChangesAsync();
            }

            // 2) örnek kategori yoksa ekle
            if (!await db.DebtTypes.AnyAsync())
            {
                db.DebtTypes.AddRange(
                    new DebtType { Name = "Faturalar" },
                    new DebtType { Name = "Vergiler" },
                    new DebtType { Name = "Krediler" }
                );
                await db.SaveChangesAsync();
            }

            // 3) örnek borç yoksa bu ay için ekle
            if (!await db.Debts.AnyAsync())
            {
                var now = DateTime.Today;
                var monthStart = new DateTime(now.Year, now.Month, 1);

                var faturalar = await db.DebtTypes.FirstAsync(x => x.Name == "Faturalar");
                var vergiler = await db.DebtTypes.FirstAsync(x => x.Name == "Vergiler");

                db.Debts.AddRange(
                    new Debt
                    {
                        DebtTypeId = faturalar.Id,
                        Name = "Elektrik",
                        Amount = 1250,
                        DueDate = monthStart.AddDays(5),
                        Payee = "Elektrik Kurumu",
                        IsPaid = false,
                        UpdatedDate = DateTime.Now
                    },
                    new Debt
                    {
                        DebtTypeId = faturalar.Id,
                        Name = "İnternet",
                        Amount = 450,
                        DueDate = monthStart.AddDays(10),
                        Payee = "İnternet Sağlayıcı",
                        IsPaid = true,
                        UpdatedDate = DateTime.Now
                    },
                    new Debt
                    {
                        DebtTypeId = vergiler.Id,
                        Name = "KDV",
                        Amount = 2200,
                        DueDate = monthStart.AddDays(18),
                        Payee = "Vergi Dairesi",
                        IsPaid = false,
                        UpdatedDate = DateTime.Now
                    }
                );

                await db.SaveChangesAsync();
            }
        }
    }
}