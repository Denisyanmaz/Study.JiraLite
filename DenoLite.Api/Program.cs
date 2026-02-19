using DenoLite.Api.Filters;
using DenoLite.Application.Interfaces;
using DenoLite.Infrastructure.Persistence;
using DenoLite.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using dotenv.net;


// ✅ VERY IMPORTANT: stop ASP.NET from remapping JWT claims (sub → NameIdentifier)
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

if (string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase))
{
    DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { ".env.production" }));
}
else
{
    DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { ".env.development" }));
}

var builder = WebApplication.CreateBuilder(args);

// ✅ Allow user-secrets for local runs even in Production environment
// (User-secrets exist only on your machine; server won't have them anyway.)
builder.Configuration.AddUserSecrets<Program>(optional: true);
// 🔹 Add services to the container
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            };
            return new BadRequestObjectResult(problem);
        };
    });
// ✅ FluentValidation (correct placement)
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddValidatorsFromAssemblyContaining<DenoLite.Application.Validation.RegisterUserDtoValidator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// 🔹 NSwag / Swagger UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "DenoLite API";

    config.AddSecurity("JWT", Enumerable.Empty<string>(), new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter 'Bearer {your token here}'"
    });
});

// 🔹 Database
// 🔹 Database (always Postgres; tests override the connection string)
builder.Services.AddDbContext<DenoLiteDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// 🔹 Dependency Injection
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<ICommentService, CommentService>();


// 🔹 JWT Authentication
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false; // ✅ critical fix

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateLifetime = true,

        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuerSigningKey = true,

        // ✅ Explicit claim mapping
        NameClaimType = "id",
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddTransient<DenoLite.Api.Middleware.ExceptionHandlingMiddleware>();
builder.Services.AddAuthorization();

var app = builder.Build();

// 🔹 Middleware pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseMiddleware<DenoLite.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
