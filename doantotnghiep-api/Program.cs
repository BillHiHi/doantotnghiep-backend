using System.Text;
using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ====================================================================
// FIX: Định dạng DateTime cho PostgreSQL (Supabase)
// ====================================================================
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// 1️⃣ DATABASE CONFIGURATION
// ====================================================================
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("❌ Missing database connection string");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ====================================================================
// 2️⃣ APPLICATION SERVICES
// ====================================================================
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<MovieStatusUpdateService>();

// ====================================================================
// 3️⃣ CACHING & COMPRESSION
// ====================================================================
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ====================================================================
// 4️⃣ CONTROLLERS & JSON SERIALIZATION
// ====================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

// ====================================================================
// 5️⃣ SWAGGER WITH JWT SUPPORT
// ====================================================================
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "🔐 Nhập: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ====================================================================
// 6️⃣ CORS CONFIGURATION
// ====================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Lưu ý: Có thể thay bằng domain cụ thể khi deploy
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// ====================================================================
// 7️⃣ & 8️⃣ AUTHENTICATION CONFIGURATION (JWT + GOOGLE)
// ====================================================================
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SecretKeyToDefendYourAPI2026";

// Gộp chung cấu hình Authentication để tránh lỗi ghi đè Scheme
var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Tích hợp Google nếu có cấu hình trong Secrets hoặc appsettings
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/api/auth/google/callback";
    });
}

builder.Services.AddAuthorization();

// ====================================================================
// 9️⃣ SIGNALR
// ====================================================================
builder.Services.AddSignalR();

// ====================================================================
// BUILD APPLICATION
// ====================================================================
var app = builder.Build();

// ====================================================================
// 🔟 MIDDLEWARE PIPELINE (THỨ TỰ CÓ QUAN TRỌNG!)
// ====================================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseRouting();

// CORS phải nằm sau UseRouting và trước UseAuthentication
app.UseCors("AllowFrontend");

app.UseResponseCaching();

// Authentication trước Authorization
app.UseAuthentication();
app.UseAuthorization();

// ====================================================================
// 1️⃣1️⃣ ENDPOINT MAPPING
// ====================================================================
app.MapControllers();
app.MapHub<BookingHub>("/bookings");
app.MapGet("/", () => "✅ Backend Cinema API is running... 🚀");

// ====================================================================
// 1️⃣2️⃣ DATABASE INITIALIZATION (Auto-migrate)
// ====================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        Console.WriteLine("✅ Database updated successfully!");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Lỗi khi khởi tạo hoặc Migrate Database");
    }
}

app.Run();