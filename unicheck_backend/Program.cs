using System.Text;
using unicheck_backend.Data;
using unicheck_backend.Models.Entities;
using unicheck_backend.Models.Enums;
// using unicheck_backend.Hubs;
using unicheck_backend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// 1. DATABASE – Entity Framework Core + SQL Server
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// ─────────────────────────────────────────────────────────────────────────────
// 2. CONCRETE SERVICES
// Ghi chú: Dùng concrete class thay vì Repository Pattern để đơn giản hóa cho KLTN
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddHttpClient("Cloudinary", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<LocalLeaveAttachmentStorage>();
builder.Services.AddScoped<CloudinaryLeaveAttachmentStorage>();
builder.Services.AddScoped<ILeaveAttachmentStorage>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var cloudinaryEnabled = config.GetValue<bool>("Cloudinary:Enabled");

    if (cloudinaryEnabled && CloudinaryLeaveAttachmentStorage.HasRequiredConfig(config))
    {
        return sp.GetRequiredService<CloudinaryLeaveAttachmentStorage>();
    }

    return sp.GetRequiredService<LocalLeaveAttachmentStorage>();
});
builder.Services.AddScoped<QrCodeService>();
builder.Services.AddScoped<FaceService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddSingleton<GpsService>();

// AttendanceStateService – Scoped per Blazor circuit (per browser tab)
builder.Services.AddScoped<AttendanceStateService>();

// AttendanceNotifier – Singleton event broker (bridges API → Blazor circuit)
builder.Services.AddSingleton<AttendanceNotifier>();

// HttpClient cho FaceService gọi Python AI service (port 8000)
builder.Services.AddHttpClient("FaceAI", client =>
{
    // var aiUrl = builder.Configuration["FaceAI:BaseUrl"] ?? "http://localhost:8000";
    var aiUrl = "https://unicheck.loca.lt/";

    client.BaseAddress = new Uri(aiUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ─────────────────────────────────────────────────────────────────────────────
// 3. AUTHENTICATION – Dual scheme: Cookie (Blazor Web GV) + JWT Bearer (Mobile API)
// ─────────────────────────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey   = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath        = "/login";
    options.LogoutPath       = "/Auth/Logout";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.Options.LoginPath);
            return Task.CompletedTask;
        }
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken            = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidAudience            = jwtSettings["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(secretKey),
        ClockSkew                = TimeSpan.Zero,
    };
});

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────────────────────────────────────
// 4. BLAZOR SERVER + MVC (Controllers cho REST API + Auth redirect)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllersWithViews();

// ─────────────────────────────────────────────────────────────────────────────
// 5. REST API – Swagger/OpenAPI (dùng cho Mobile / Flutter)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "UniCheck API",
        Version     = "v1",
        Description = "REST API cho Mobile Flutter – Hệ thống điểm danh sinh viên UniCheck",
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Description  = "Nhập JWT token. Ví dụ: Bearer eyJhbGci...",
        Reference    = new OpenApiReference
        {
            Id   = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme,
        },
    };
    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// 6. REAL-TIME – SignalR (dùng bởi Blazor Server tự động)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─────────────────────────────────────────────────────────────────────────────
// 7. SESSION
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

// ─────────────────────────────────────────────────────────────────────────────
// BUILD
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// 8. MIDDLEWARE PIPELINE
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UniCheck API v1");
        c.RoutePrefix = "swagger";
    });

    // Seed dữ liệu demo khi chạy Development (idempotent — chỉ seed nếu DB rỗng)
    await DbSeeder.SeedAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ─────────────────────────────────────────────────────────────────────────────
// 9. ENDPOINTS
// ─────────────────────────────────────────────────────────────────────────────

// REST API Controllers (prefix /api/**)
app.MapControllers();

// MVC fallback cho Auth redirect
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Blazor Server – Toàn bộ UI giảng viên
app.MapRazorComponents<unicheck_backend.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
