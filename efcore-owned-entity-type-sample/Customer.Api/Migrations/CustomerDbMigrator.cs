using FluentMigrator.Runner;

namespace Customer.Api.Migrations;

public class CustomerDbMigrator
{
    public static void Run(IServiceProvider serviceProvider)
    {
        // Instantiate the runner
        var runner = serviceProvider.GetRequiredService<IMigrationRunner>();

        // Execute the migrations
        runner.MigrateUp();
    }
}
