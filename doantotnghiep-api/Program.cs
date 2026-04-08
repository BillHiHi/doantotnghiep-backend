using System.Security.Cryptography;
using System.Text;
using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Fix lỗi định dạng thời gian cho PostgreSQL (Supabase)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ================= 1. CẤU HÌNH DATABASE =================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing DB connection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ================= 2. CÁC SERVICES HỆ THỐNG =================
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<MovieStatusUpdateService>(); 

// ⭐ ELITE OPTIMIZATION: Bật Nén Phản Hồi (Gzip/Brotli) - Giảm size JSON cực mạnh
builder.Services.AddResponseCompression(options => {
    options.EnableForHttps = true;
});

// ⭐ ELITE OPTIMIZATION: Bật In-Memory Cache
builder.Services.AddMemoryCache();

// ⭐ ELITE OPTIMIZATION: Bật Response Caching Service (Cần thiết cho VaryByQueryKeys)
builder.Services.AddResponseCaching();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

// ================= 3. SWAGGER + JWT CỦA SWAGGER =================
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {token}"
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

// ================= 4. CẤU HÌNH CORS (CHO PHÉP VUEJS GỌI API) =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Trình duyệt sẽ nhớ quyền CORS trong 10p, không hỏi lại liên tục
    });
});

// ================= 5. CẤU HÌNH JWT AUTHENTICATION =================
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SecretKeyToDefendYourAPI2026"))
        };
    });

builder.Services.AddAuthorization();

// ================= 6. SIGNALR =================
builder.Services.AddSignalR();

// ================================================
var app = builder.Build();
// ================================================

// ================= 7. MIDDLEWARE PIPELINE (THỨ TỰ RẤT QUAN TRỌNG) =================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// FIX LỖI 404 ẢNH: Phải nằm trước UseRouting
app.UseStaticFiles();

// Nén response JSON/HTML...
app.UseResponseCompression();

app.UseHttpsRedirection();
app.UseRouting();

// FIX LỖI CORS: Phải nằm giữa UseRouting và UseAuthentication
app.UseCors("AllowFrontend");

// ⭐ ELITE OPTIMIZATION: Bật Middleware Response Caching (Nên nằm sau CORS)
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

// ĐỊNH TUYẾN API & SIGNALR
app.MapControllers();
app.MapHub<BookingHub>("/bookings");

// HEALTH CHECK ĐỂ RENDER KIỂM TRA TRẠNG THÁI APP
app.MapGet("/", () => "Backend Cinema API is running... 🚀");
// ================= 8. AUTO MIGRATE (TỰ ĐỘNG TẠO BẢNG) =================
// Lưu ý: Đoạn này sẽ giúp bạn tự tạo bảng trên Supabase khi deploy
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // await context.Database.MigrateAsync(); // Bỏ comment nếu muốn tự động tạo bảng
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi khởi tạo Database");
    }
}

app.Run();