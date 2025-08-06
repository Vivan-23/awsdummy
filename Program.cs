using awsdummy;
using awsdummy.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Metadata;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Your API", Version = "v1" });

    // Enable JWT auth
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token like: Bearer {your token here}"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    var securityRequirement = new OpenApiSecurityRequirement
    {
        {
            securityScheme,
            new string[] {}
        }
    };

    c.AddSecurityRequirement(securityRequirement);
});


List<SamlConfig> samlConfigs = new();
string connStr = builder.Configuration.GetConnectionString("DefaultConnection");

using (var connection = new MySqlConnection(connStr))
{
    connection.Open();
    string sql = "SELECT Id, MetadataLocation, EntityId FROM SamlSettings";

    using var cmd = new MySqlCommand(sql, connection);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        samlConfigs.Add(new SamlConfig
        {
            Id = reader.GetInt32(0),
            MetadataLocation = reader.GetString(1),
            EntityId = reader.GetString(2)
        });
    }
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = ApplicationSamlConstants.Application;
    options.DefaultSignInScheme = ApplicationSamlConstants.External;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
// ✅ JWT Bearer Authentication
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new Exception("Missing Jwt:Key in configuration");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(
            Convert.ToDouble(builder.Configuration["Jwt:ExpireInMinutes"] ?? "5")
        ),
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    };
})

// ✅ Application Cookie (holds SAML claims after login)
.AddCookie(ApplicationSamlConstants.Application)
// ✅ External Cookie (optional for temporary identities)
.AddCookie(ApplicationSamlConstants.External)

// ✅ AWS Cognito SAML2 Configuration
.AddSaml2("Saml2Aws", options =>
{
    options.SignInScheme = ApplicationSamlConstants.Application;

    options.SPOptions.EntityId = new EntityId("https://localhost:7191/Saml2Aws");
    options.SPOptions.ReturnUrl = new Uri("https://localhost:7191/api/auth/callbackaws");

    var awsSaml = samlConfigs.FirstOrDefault(s => s.Id == 1); // Or filter by ID, domain, etc.

    if (awsSaml is not null)
    {
        options.IdentityProviders.Add(new IdentityProvider(
            new EntityId(awsSaml.EntityId),
            options.SPOptions)
        {
            MetadataLocation = awsSaml.MetadataLocation,
            LoadMetadata = true,
            AllowUnsolicitedAuthnResponse = true
        });
    }
    else
    {
        Console.WriteLine("⚠ No SAML configuration found in DB.");
    }
});
builder.Services.AddScoped<JwtService>();
builder.Services.AddControllersWithViews(); // Enables controllers + views
builder.Services.AddRazorPages();
builder.Services.AddSession();
var app = builder.Build();
app.UseSession();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
public class SamlConfig
{
    public int Id { get; set; }
    public string MetadataLocation { get; set; }
    public string EntityId { get; set; }
}