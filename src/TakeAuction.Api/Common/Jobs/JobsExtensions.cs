using System.Reflection;
using Hangfire;
using Hangfire.PostgreSql;

namespace TakeAuction.Api.Common.Jobs;

public static class JobsExtensions
{
    public const string DashboardRoute = "/hangfire";

    public static IServiceCollection AddTakeAuctionJobs(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var section = configuration.GetSection(JobOptions.SectionName);

        services.AddOptions<JobOptions>().Bind(section);

        var options = section.Get<JobOptions>() ?? new JobOptions();
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                postgres => postgres.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = options.SchemaName,
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(options.QueuePollIntervalSeconds),
                    InvisibilityTimeout = TimeSpan.FromMinutes(5),
                    DistributedLockTimeout = TimeSpan.FromMinutes(1)
                }));

        if (options.ServerEnabled)
        {
            services.AddHangfireServer(server =>
            {
                server.WorkerCount = options.WorkerCount;
                server.ServerName = $"takeauction-{Environment.MachineName}";
            });
        }

        foreach (var type in assembly.DefinedTypes.Where(IsRecurringJobRegistration))
        {
            services.Add(ServiceDescriptor.Transient(typeof(IRecurringJobRegistration), type));
        }

        return services;
    }

    public static IApplicationBuilder UseTakeAuctionRecurringJobs(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        foreach (var registration in scope.ServiceProvider.GetServices<IRecurringJobRegistration>())
        {
            registration.Register(manager);
        }

        return app;
    }

    public static IApplicationBuilder UseTakeAuctionJobsDashboard(this IApplicationBuilder app)
    {
        app.UseHangfireDashboard(DashboardRoute, new DashboardOptions
        {
            DisplayStorageConnectionString = false,
            DarkModeEnabled = true,
            Authorization = [new AdminOnlyDashboardFilter()]
        });

        return app;
    }

    private static bool IsRecurringJobRegistration(TypeInfo type) =>
        type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
        && type.IsAssignableTo(typeof(IRecurringJobRegistration));
}
