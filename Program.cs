using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuizMasterServer.Data;
using QuizMasterServer.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// הגדרה לעבודה מאחורי proxy (Render)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var mongoConnection = Environment.GetEnvironmentVariable("MONGODB_CONNECTION")
                      ?? builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;

var mongoDatabase = Environment.GetEnvironmentVariable("MONGODB_DB")
                    ?? builder.Configuration.GetSection("MongoDbSettings:DatabaseName").Value;

builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = mongoConnection;
    options.DatabaseName = mongoDatabase;
});

builder.Services.AddScoped<IMongoDbContext, MongoDbContext>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Authentication - רק JWT + Google (ללא Cookie)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? "MyVerySecretJwtKeyThatIsAtLeast32CharsLong";
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "https://quizmasterserver.onrender.com";
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "QuizMasterClient";

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
})
.AddGoogle(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                       ?? builder.Configuration["Google:ClientId"];
    options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                           ?? builder.Configuration["Google:ClientSecret"];
    options.CallbackPath = "/api/auth/google-callback";

    // Google OAuth יעבוד עם state שנשמר בזיכרון בלבד
    options.SaveTokens = true;
    options.Scope.Add("email");
    options.Scope.Add("profile");

    // אין צורך ב-SignInScheme כי אנחנו לא משתמשים ב-Cookies
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TeacherOnly", p => p.RequireRole("Teacher"));
    options.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
    options.AddPolicy("TeacherOrStudent", p => p.RequireRole("Student", "Teacher"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "QuizMaster API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token",
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',')
                           ?? new[] { "http://localhost:3000", "https://quizmastersystem.netlify.app" };

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

// חשוב! ForwardedHeaders לפני כל דבר אחר
app.UseForwardedHeaders();

app.UseStaticFiles();
app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();