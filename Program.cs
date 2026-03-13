using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ChatAPI.Data;
using ChatAPI.Hubs;
using ChatAPI.Services;

var builder = WebApplication.CreateBuilder(args);

var mysqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST");
var mysqlPort = Environment.GetEnvironmentVariable("MYSQL_PORT");
var mysqlDb   = Environment.GetEnvironmentVariable("MYSQL_DB");
var mysqlUser = Environment.GetEnvironmentVariable("MYSQL_USER");
var mysqlPass = Environment.GetEnvironmentVariable("MYSQL_PASS");
var connStr   = $"Server={mysqlHost};Port={mysqlPort};Database={mysqlDb};User={mysqlUser};Password={mysqlPass};";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

builder.Services.AddSingleton<CryptoService>();
builder.Services.AddSingleton<CloudinaryService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var t = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = t;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p =>
    p.WithOrigins(
        "http://localhost:3000",
        "https://spectacular-marzipan-10e3ff.netlify.app"
    )
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("✅ Base de datos lista");
    }
    catch (Exception ex) { Console.WriteLine($"❌ Error BD: {ex.Message}"); }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/", () => Results.Redirect("/swagger"));

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
