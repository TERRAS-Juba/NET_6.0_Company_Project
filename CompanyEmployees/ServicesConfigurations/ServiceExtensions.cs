using AspNetCoreRateLimit;
using CompanyEmployees.ActionFilters;
using CompanyEmployees.Formatters;
using Contracts;
using Entities;
using Entities.DataTransferObjects;
using Marvin.Cache.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog.Web;
using Npgsql;
using Repository;
using Repository.DataShaping;

namespace CompanyEmployees.ServicesConfigurations;

public static class ServiceExtensions
{
    public static void ConfigureDIForAutoMapper(this IServiceCollection services)
    {
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
    }

    public static void ConfigureDIForRepositoryBase(this IServiceCollection services)
    {
        services.AddScoped<IRepositoryManager, RepositoryManager>();
    }

    public static void ConfigurePostgresDatabaseConnection(this WebApplicationBuilder builder, string connectionString,
        string migrationAssembly)
    {
        var connectionBuilder = new NpgsqlConnectionStringBuilder();
        connectionBuilder.ConnectionString = builder.Configuration.GetConnectionString(connectionString);
        builder.Services.AddDbContext<RepositoryContext>(o =>
            o.UseNpgsql(connectionBuilder.ConnectionString, b => b.MigrationsAssembly(migrationAssembly)));
    }

    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Host.UseNLog();
    }

    public static void ConfigureFilters(this IServiceCollection services)
    {
        services.AddScoped<ValidationFilterAttribute>();
        services.AddScoped<ValidateCompanyExistsAttribute>();
    }

    public static void ConfigureDIForDataShaper(this IServiceCollection services)
    {
        services.AddScoped<IDataShaper<EmployeeDto>, DataShaper<EmployeeDto>>();
        services.AddScoped<IDataShaper<CompanyDto>, DataShaper<CompanyDto>>();
        services.AddScoped<IDataShaper<CompanyJoinEmployeeDto>, DataShaper<CompanyJoinEmployeeDto>>();
    }

    public static void ConfigureAPIVersionning(this IServiceCollection services)
    {
        services.AddApiVersioning(opt =>
            {
                opt.ReportApiVersions = true;
                opt.AssumeDefaultVersionWhenUnspecified = true;
                opt.DefaultApiVersion = new ApiVersion(1, 0);
            }
        );
        // Replace error 400 with 422 when validation rules of incoming dto is not valide
        // 400 => Bad request
        // 422 => Unprocessable Entity
        services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });
    }

    public static void ConfigureCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
            options.AddPolicy("CorsPolicy", build => build.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
    }

    public static void ConfigureControllersBehaviour(this IServiceCollection services)
    {
        services.AddControllers(config =>
                {
                    // Content Negotiation
                    config.RespectBrowserAcceptHeader = true;
                    config.ReturnHttpNotAcceptable = true;
                }
                //Added support for XML format for requests 
            ).AddXmlDataContractSerializerFormatters()
            // Added support for patch requests
            .AddNewtonsoftJson();
    }

    public static void ConfigureCustomOutputFormaters(this IServiceCollection services)
    {
        services.AddMvc(config => config.OutputFormatters.Add(new CsvOutputFormatter()));
    }

    public static void ConfigureResponseCaching(this IServiceCollection services)
    {
        services.AddResponseCaching();
    }

    public static void ConfigureHttpCacheHeaders(this IServiceCollection services)
    {
        services.AddHttpCacheHeaders((expirationOpt) =>
            {
                expirationOpt.MaxAge = 20;
                expirationOpt.CacheLocation = CacheLocation.Public;
            },
            (validationOpt) => { validationOpt.MustRevalidate = true; });
    }

    public static void ConfigureRateLimitingOptions(this IServiceCollection services)
    {
        var rateLimitRules = new List<RateLimitRule>()
        {
            new()
            {
                Endpoint = "*",
                Limit = 3,
                Period = "5m"
            }
        };
        services.Configure<IpRateLimitOptions>(opt =>
        {
            opt.GeneralRules = rateLimitRules;
        });
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    }
}