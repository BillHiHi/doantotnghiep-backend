using System.Text;
using System.Security.Cryptography;
using doantotnghiep_api.Data;
using doantotnghiep_api.Hubs;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
            new string[] {}
        }
    });
});


// ================= Database =================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


// ================= CORS =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://rapfim.vercel.app"
            )
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
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

app.UseCors("AllowFrontend");

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
    {
        Mappings = { [".avif"] = "image/avif" }
    }
});

app.UseAuthentication(); // MUST BEFORE Authorization
app.UseAuthorization();

app.MapControllers();
app.MapHub<BookingHub>("/Bookings");


// ========================================
// AUTO MIGRATE + SEED ADMIN
// ========================================

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    context.Database.Migrate();

    if (!context.Users.Any(x => x.Role == "Admin"))
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

        context.SaveChanges();

        Console.WriteLine("🔥 Admin created:");
        Console.WriteLine("Email: admin@cinema.com");
        Console.WriteLine("Password: 123456");
    }
}

app.Run();
