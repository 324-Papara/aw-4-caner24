using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using AutoMapper;
using FluentValidation.AspNetCore;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Para.Api;
using Para.Api.Jobs;
using Para.Api.Middleware;
using Para.Api.Service;
using Para.Base;
using Para.Base.Token;
using Para.Bussiness;
using Para.Bussiness.Cqrs;
using Para.Bussiness.MessageQuakers.RabbitMQ.Abstract;
using Para.Bussiness.MessageQuakers.RabbitMQ.Concrete;
using Para.Bussiness.Notification;
using Para.Bussiness.Token;
using Para.Bussiness.Validation;
using Para.Data.Context;
using Para.Data.UnitOfWork;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configuration ve Logger ayarlarý
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();
Log.Information("Application is starting...");

// Servisleri ekleme
builder.Services.AddSingleton(config.GetSection("JwtConfig").Get<JwtConfig>());
builder.Services.AddDbContext<ParaDbContext>(options =>
    options.UseSqlServer(config.GetConnectionString("MsSqlConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddControllers().AddFluentValidation(x =>
{
    x.RegisterValidatorsFromAssemblyContaining<BaseValidator>();
});

builder.Services.AddSingleton(new MapperConfiguration(cfg =>
{
    cfg.AddProfile(new MapperConfig());
}).CreateMapper());

builder.Services.AddMediatR(typeof(CreateCustomerCommand).GetTypeInfo().Assembly);

builder.Services.AddTransient<CustomService1>();
builder.Services.AddScoped<CustomService2>();
builder.Services.AddSingleton<CustomService3>();
builder.Services.AddScoped<IJobEmailService, JobEmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddScoped<IMessageProducer, RabbitMQProducer>();

//builder.Services.AddHostedService<ContinuousConsumeService>();


builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = config["JwtConfig:Issuer"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(config["JwtConfig:Secret"])),
        ValidAudience = config["JwtConfig:Audience"],
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Para Api Management", Version = "v1.0" });
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Para Management for IT Company",
        Description = "Enter JWT Bearer token **_only_**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });
});

builder.Services.AddMemoryCache();

var redisConfig = new ConfigurationOptions
{
    DefaultDatabase = 0,
    EndPoints = { { config["Redis:Host"], Convert.ToInt32(config["Redis:Port"]) } }
};
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.ConfigurationOptions = redisConfig;
    opt.InstanceName = config["Redis:InstanceName"];
});

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(config.GetConnectionString("HangfireConnection")));
builder.Services.AddHangfireServer();

builder.Services.AddScoped<ISessionContext>(provider =>
{
    var context = provider.GetService<IHttpContextAccessor>();
    var sessionContext = new SessionContext
    {
        Session = JwtManager.GetSession(context.HttpContext),
        HttpContext = context.HttpContext
    };
    return sessionContext;
});

builder.Services.AddSingleton<ContinuousConsumeService>();

var app = builder.Build();


var consumeService = app.Services.GetService<ContinuousConsumeService>();
var backgroundJobs = app.Services.GetService<IBackgroundJobClient>();

backgroundJobs.Enqueue(() => consumeService.ProcessMessages());

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Para.Api v1"));
}

app.UseMiddleware<HeartbeatMiddleware>();
app.UseMiddleware<ErrorHandlerMiddleware>();
Action<RequestProfilerModel> requestResponseHandler = requestProfilerModel =>
{
    Log.Information("-------------Request-Begin------------");
    Log.Information(requestProfilerModel.Request);
    Log.Information(Environment.NewLine);
    Log.Information(requestProfilerModel.Response);
    Log.Information("-------------Request-End------------");
};
app.UseMiddleware<RequestLoggingMiddleware>(requestResponseHandler);

app.UseHangfireDashboard();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

app.Use(async (context, next) =>
{
    if (!string.IsNullOrEmpty(context.Request.Path) && context.Request.Path.Value.Contains("favicon"))
    {
        await next();
        return;
    }

    var service1 = context.RequestServices.GetRequiredService<CustomService1>();
    var service2 = context.RequestServices.GetRequiredService<CustomService2>();
    var service3 = context.RequestServices.GetRequiredService<CustomService3>();

    service1.Counter++;
    service2.Counter++;
    service3.Counter++;

    await next();
});

app.Run(async context =>
{
    var service1 = context.RequestServices.GetRequiredService<CustomService1>();
    var service2 = context.RequestServices.GetRequiredService<CustomService2>();
    var service3 = context.RequestServices.GetRequiredService<CustomService3>();

    if (!string.IsNullOrEmpty(context.Request.Path) && !context.Request.Path.Value.Contains("favicon"))
    {
        service1.Counter++;
        service2.Counter++;
        service3.Counter++;
    }

    await context.Response.WriteAsync($"Service1 : {service1.Counter}\n");
    await context.Response.WriteAsync($"Service2 : {service2.Counter}\n");
    await context.Response.WriteAsync($"Service3 : {service3.Counter}\n");
});

app.Run();
