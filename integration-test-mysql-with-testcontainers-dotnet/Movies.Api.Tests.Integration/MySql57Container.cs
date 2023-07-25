using System;
using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestContainers.Container.Abstractions.Images;
using TestContainers.Container.Database.AdoNet.WaitStrategies;
using TestContainers.Container.Database.MySql;

namespace Movies.Api.Tests.Integration
{
	public class MySql57Container : MySqlContainer
	{
        private static IImage CreateDefaultImage(IDockerClient dockerClient, ILoggerFactory loggerFactory) =>
            new GenericImage(dockerClient, loggerFactory) { ImageName = $"{MySqlContainer.DefaultImage}:5.7" };

        [ActivatorUtilitiesConstructor]
        public MySql57Container(IImage dockerImage,IDockerClient dockerClient, ILoggerFactory loggerFactory)
			: base(NullImage.IsNullImage(dockerImage) ? CreateDefaultImage(dockerClient, loggerFactory) : dockerImage, dockerClient, loggerFactory)
		{ }

        protected override async Task ConfigureAsync()
        {
            await base.ConfigureAsync();

            var adoNetSqlProbeStrategy = new AdoNetSqlProbeStrategy(DbProviderFactory);
            adoNetSqlProbeStrategy.Timeout = TimeSpan.FromMinutes(2);
            WaitStrategy = adoNetSqlProbeStrategy;
        }
    }
}

