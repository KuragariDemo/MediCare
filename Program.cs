using MediCare.App.Data;
using MediCare.App.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MediCare.App.Services.Scheduling;

var builder = WebApplication.CreateBuilder(args);

// Cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// --- DbContext ---
builder.Services.AddDbContext<MediCareContext>(o =>
{
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql =>
        {
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            sql.CommandTimeout(120); // seconds
        });
});

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
{
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<MediCareContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// --- Authorization Policies ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPlusOnly", p => p.RequireRole("AdminPlus"));
    options.AddPolicy("CanCreateAccounts", p => p.RequireRole("AdminPlus"));
});

// Several AJAX views send the antiforgery token via a "RequestVerificationToken" header
// (see Views/Admin/DoctorSpecialties.cshtml, Views/Patient/MyAppointments.cshtml, etc.).
// Without this, [ValidateAntiForgeryToken] only looks at a form field and those header-only
// requests are rejected.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IScheduleService, ScheduleService>();

var app = builder.Build();

// --- Middleware ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --- Routes ---
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "doctor",
    pattern: "Doctors/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "patient",
    pattern: "Patient/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// --- Ensure DB is ready, then seed ---
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<MediCareContext>();

    // 1) Apply migrations BEFORE touching Identity tables
    try
    {
        logger.LogInformation("Applying migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply migrations.");
        throw; // let the app crash here instead of hanging later
    }

    // 2) Seed roles + root admin with a generous timeout
    try
    {
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        foreach (var r in new[] { "AdminPlus", "Admin", "Doctor", "Patient" })
        {
            if (!await roleMgr.RoleExistsAsync(r))
            {
                var create = await roleMgr.CreateAsync(new IdentityRole(r));
                if (!create.Succeeded)
                    logger.LogWarning("Failed creating role {Role}: {Errors}", r, string.Join(", ", create.Errors.Select(e => e.Description)));
            }
        }

        // Seed root AdminPlus. Credentials come from configuration (appsettings / environment
        // variables / user-secrets) rather than being hardcoded, so no real password lives in
        // source control. In Development a fallback is used for convenience only.
        var rootEmail = builder.Configuration["Seed:RootAdmin:Email"] ?? "root.adminplus@medicare.local";
        var rootPassword = builder.Configuration["Seed:RootAdmin:Password"];
        if (string.IsNullOrWhiteSpace(rootPassword))
        {
            if (app.Environment.IsDevelopment())
            {
                rootPassword = "Root@12345";
                logger.LogWarning("Seed:RootAdmin:Password not configured; using the Development-only default. Set it via appsettings, an environment variable, or user-secrets for anything beyond local dev.");
            }
            else
            {
                throw new InvalidOperationException("Seed:RootAdmin:Password must be set via configuration (environment variable MediCare__Seed__RootAdmin__Password, appsettings, or a secret store) before the app can seed the root admin account.");
            }
        }

        var root = await userMgr.FindByEmailAsync(rootEmail);
        if (root == null)
        {
            root = new ApplicationUser
            {
                UserName = rootEmail,
                Email = rootEmail,
                FullName = "Root AdminPlus",
                EmailConfirmed = true
            };

            var created = await userMgr.CreateAsync(root, rootPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("Failed to create root admin: " + string.Join(", ", created.Errors.Select(e => e.Description)));

            await userMgr.AddToRoleAsync(root, "AdminPlus");

            db.AdminProfiles.Add(new AdminProfile
            {
                UserId = root.Id,
                Level = AdminLevel.AdminPlus,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = root.Id
            });
            await db.SaveChangesAsync(cts.Token);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seeding failed.");
        throw;
    }
}

app.Run();
