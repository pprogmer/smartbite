using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartBite.Api.Services;
using SmartBite.Data;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddControllers()
    // Enums travel by name only, so undefined numeric values never reach the database.
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false)
        )
    );
builder.Services.AddDbContext<SmartBiteDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("SmartBite"))
);
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Keep "sub" as is instead of mapping it to the long XML claim name.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<OpenRouterOptions>(builder.Configuration.GetSection("OpenRouter"));
builder.Services.AddHttpClient<OpenRouterClient>(
    (services, http) =>
        http.BaseAddress = new Uri(
            services.GetRequiredService<IOptions<OpenRouterOptions>>().Value.BaseUrl
        )
);
builder.Services.AddScoped<DiaryService>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<EstimateService>();

var app = builder.Build();

// Apply migrations on startup (development only).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<SmartBiteDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

// Everything except registration and sign-in needs a token.
app.MapControllers().RequireAuthorization();
app.Run();
