using API_DJCONNECT.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using API_DJCONNECT.Hubs; // <--- 1. IMPORTANTE: Añade el namespace de tus Hubs

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DjConnectContext>(options =>
    options.UseNpgsql(connectionString));

// ==================================================================
// 2. CONFIGURACIÓN JWT
// ==================================================================
builder.Services.AddAuthentication(options =>
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };

    // Configuración opcional para leer el token desde la query string (útil para WebSockets si no envían header)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Agregamos controladores con la opción de ignorar ciclos
builder.Services.AddControllers().AddJsonOptions(x =>
   x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// ==================================================================
// 3. SIGNALR (NUEVO)
// ==================================================================
builder.Services.AddSignalR(); // <--- 2. Registramos el servicio

// 4. Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DJ Connect API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// ==================================================================
// 5. CORS ACTUALIZADO (CRÍTICO PARA SIGNALR)
// ==================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              // SignalR requiere credenciales, y AllowAnyOrigin no funciona con AllowCredentials.
              // Usamos SetIsOriginAllowed para permitir todo en desarrollo.
              .SetIsOriginAllowed((host) => true)
              .AllowCredentials(); // <--- NECESARIO para que el socket se conecte
    });
});

builder.Services.AddScoped<API_DJCONNECT.Services.CloudinaryService>();

var app = builder.Build();

// ORDEN DE MIDDLEWARES
app.UseCors("AllowAll"); // CORS va primero

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ==================================================================
// 6. MAPEO DEL HUB (NUEVO)
// ==================================================================
app.MapHub<ChatHub>("/chathub"); // <--- 3. La ruta donde escuchará el chat

app.Run();