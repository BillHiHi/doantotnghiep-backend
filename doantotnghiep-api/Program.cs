using System.Security.Cryptography;
using System.Text;
using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

using doantotnghiep_api.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ================= Swagger + Registration =================
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<doantotnghiep_api.Services.MovieStatusUpdateService>();


// ========================================
// DATABASE CONFIG (Tối ưu kết nối Supabase)
// ========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// ========================================
// SERVICES
// ========================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();


// ================= Swagger + JWT =================
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {your token}"
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


// ================= CORS =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") 
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); 
        });
});


// ================= JWT Authentication =================
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing"))
            )
        };
    });

builder.Services.AddAuthorization();


// ================= SignalR =================
builder.Services.AddSignalR();



// ========================================
// APP PIPELINE
// ========================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("AllowFrontend");

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
    {
        Mappings = { [".avif"] = "image/avif" }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowLocalhost");
app.MapControllers();
app.MapHub<BookingHub>("/Bookings");



// ========================================
// AUTO MIGRATE + SEED ADMIN + DB FIX
// ========================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("⏳ Đang kiểm tra và cập nhật Database...");

        // 1. Tự động chạy các bản Migration của EF Core
        await context.Database.MigrateAsync();

        // 2. Khởi tạo tài khoản Admin nếu chưa có
        if (!await context.Users.AnyAsync(x => x.Role == "Admin"))
        {
            string Hash(string password)
            {
                using var sha = SHA256.Create();
                return Convert.ToBase64String(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(password))
                );
            }

            context.Users.Add(new User
            {
                Email = "admin@cinema.com",
                PasswordHash = Hash("123456"),
                FullName = "System Admin",
                PhoneNumber = "",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            logger.LogInformation("🔥 Đã tạo tài khoản Admin mặc định: admin@cinema.com / 123456");
        }

        // 3. Tự động chạy script SQL fix bảng/cột bị thiếu
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Promotions"" (
                ""PromotionId"" SERIAL PRIMARY KEY,
                ""Title"" VARCHAR(200) NOT NULL,
                ""Summary"" TEXT,
                ""Content"" TEXT,
                ""ImageUrl"" TEXT,
                ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""EndDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""IsPublished"" BOOLEAN NOT NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );

            DO $$ 
            BEGIN 
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE lower(table_name)='seatlocks' AND lower(column_name)='paymentcode') THEN
                    ALTER TABLE ""SeatLocks"" ADD COLUMN ""PaymentCode"" TEXT;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE lower(table_name)='seatlocks' AND lower(column_name)='totalamount') THEN
                    ALTER TABLE ""SeatLocks"" ADD COLUMN ""TotalAmount"" DECIMAL(18,2);
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE lower(table_name)='seatlocks' AND lower(column_name)='combos') THEN
                    ALTER TABLE ""SeatLocks"" ADD COLUMN ""Combos"" TEXT;
                END IF;
            END $$;
        ");

        logger.LogInformation("✅ Database khởi tạo và cập nhật thành công.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Có lỗi xảy ra trong quá trình khởi tạo Database.");
    }
}

app.Run();