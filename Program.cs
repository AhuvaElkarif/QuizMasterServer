using QuizMasterServer.Data;
using QuizMasterServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoDbSettings
{
    ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION") ?? "",
    DatabaseName = Environment.GetEnvironmentVariable("MONGODB_DB") ?? "DefaultDbName"
};
builder.Services.AddSingleton(mongoSettings);

// Add JwtTokenService
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Authentication Setup - קריאה מ-Environment Variables
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

    if (string.IsNullOrEmpty(jwtKey))
        throw new InvalidOperationException("JWT_KEY environment variable is not set");
    if (string.IsNullOrEmpty(jwtIssuer))
        throw new InvalidOperationException("JWT_ISSUER environment variable is not set");
    if (string.IsNullOrEmpty(jwtAudience))
        throw new InvalidOperationException("JWT_AUDIENCE environment variable is not set");

    var key = Encoding.UTF8.GetBytes(jwtKey);
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeacherOnly", p => p.RequireRole("Teacher"));
    options.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
    options.AddPolicy("TeacherOrStudent", p => p.RequireRole("Student", "Teacher"));
});

// Controllers
builder.Services.AddControllers();

// Swagger with JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ExamManagementMongoApi", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token **_only_**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme,
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { securityScheme, new string[]{ } }
    });
});

// CORS Configuration - מאפשר גישה לכולם
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()    // מאפשר לכל origin
            .AllowAnyMethod()    // מאפשר כל HTTP method
            .AllowAnyHeader();   // מאפשר כל header
        // הסרנו AllowCredentials() כי זה לא תואם עם AllowAnyOrigin()
    });
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS must come BEFORE Authentication and Authorization
app.UseCors(); // משתמש ב-default policy

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();