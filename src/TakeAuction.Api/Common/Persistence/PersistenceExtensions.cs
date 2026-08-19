using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence.Seeding;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Common.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddTakeAuctionPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.DefaultPassword),
                $"{SeedOptions.SectionName}:{nameof(SeedOptions.DefaultPassword)} is required when "
                    + $"{SeedOptions.SectionName}:{nameof(SeedOptions.Enabled)} is true. Seeding creates an "
                    + "administrator, so the password has to come from configuration, never from a default.")
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            });

            options.AddInterceptors(serviceProvider.GetRequiredService<OutboxSignalInterceptor>());

            var seedOptions = serviceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
            var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();

            options.UseAsyncSeeding((context, _, cancellationToken) =>
                DatabaseSeeder.SeedAsync(context, seedOptions, passwordHasher, cancellationToken));

            options.UseSeeding((context, _) =>
                DatabaseSeeder.SeedAsync(context, seedOptions, passwordHasher).GetAwaiter().GetResult());
        });

        return services;
    }

    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
}
