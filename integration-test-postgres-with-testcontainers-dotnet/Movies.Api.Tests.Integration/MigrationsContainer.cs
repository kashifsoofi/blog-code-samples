using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestContainers.Container.Abstractions;
using TestContainers.Container.Abstractions.Hosting;
using TestContainers.Container.Abstractions.Images;

namespace Movies.Api.Tests.Integration;


public class MigrationsContainer : GenericContainer
{
    [ActivatorUtilitiesConstructor]
    public MigrationsContainer(IDockerClient dockerClient, ILoggerFactory loggerFactory)
        : base(CreateDefaultImage(), dockerClient, loggerFactory)
    {
        this.DockerClient = dockerClient;
    }

    internal IDockerClient DockerClient { get; }

    public async Task<long> GetExitCodeAsync()
    {
        var containerWaitResponse = await this.DockerClient.Containers.WaitContainerAsync(this.ContainerId);
        return containerWaitResponse.StatusCode;
    }

    private static IImage CreateDefaultImage()
    {
        return new ImageBuilder<DockerfileImage>()
            .ConfigureImage((context, image) =>
            {
                image.DockerfilePath = "Dockerfile";
                image.DeleteOnExit = true;
                image.BasePath = "../../../../db";
            })
            .Build();
    }
}

