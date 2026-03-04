using IdeioMuhasebe.Data;
using IdeioMuhasebe.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<DatabaseContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});

// Add services to the container.
builder.Services.AddControllersWithViews(o =>
{
    // AJAX POST’larda da CSRF devrede
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddScoped<RecurringDebtService>();
builder.Services.AddHostedService<RecurringDebtHostedService>();
builder.Services.AddScoped<RecurringIncomeService>();
builder.Services.AddHostedService<RecurringIncomeHostedService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/Login";
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");




app.Run();
