// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Data.SqlTypes;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Postgres;
using Aspire.Hosting.Publishing;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding PostgreSQL resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class PostgresBuilderExtensions
{
    private const string PasswordEnvVarName = "POSTGRES_PASSWORD";

    /// <summary>
    /// Adds a PostgreSQL container to the application model. The default image is "postgres" and the tag is "latest".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for PostgreSQL.</param>
    /// <param name="password">The password for the PostgreSQL container. Defaults to a random password.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostgresContainerResource}"/>.</returns>
    public static IResourceBuilder<PostgresContainerResource> AddPostgresContainer(this IDistributedApplicationBuilder builder, string name, int? port = null, string? password = null)
    {
        password = password ?? Guid.NewGuid().ToString("N");
        var postgresContainer = new PostgresContainerResource(name, password);
        return builder.AddResource(postgresContainer)
                      .WithManifestPublishingCallback(context => WritePostgresContainerResourceToManifest(context, postgresContainer))
                      .WithAnnotation(new EndpointAnnotation(ProtocolType.Tcp, port: port, containerPort: 5432)) // Internal port is always 5432.
                      .WithAnnotation(new ContainerImageAnnotation { Image = "postgres", Tag = "latest" })
                      .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "scram-sha-256")
                      .WithEnvironment("POSTGRES_INITDB_ARGS", "--auth-host=scram-sha-256 --auth-local=scram-sha-256")
                      .WithEnvironment(context =>
                      {
                          if (context.PublisherName == "manifest")
                          {
                              context.EnvironmentVariables.Add(PasswordEnvVarName, $"{{{postgresContainer.Name}.inputs.password}}");
                          }
                          else
                          {
                              context.EnvironmentVariables.Add(PasswordEnvVarName, postgresContainer.Password);
                          }
                      });
    }

    public static IResourceBuilder<ContainerResource> AsContainer(this IResourceBuilder<PostgresServerResource> postgresServer)
    {
        var abstractResource = (IAbstractResource<IPostgresServiceResource>)postgresServer.Resource;
        if (abstractResource.Implementation is { } implementation)
        {
            if (implementation is ContainerResource container)
            {
                return new DistributedApplicationResourceBuilder<ContainerResource>(postgresServer.ApplicationBuilder, container);
            }
            else
            {
                throw new InvalidOperationException($"Resource {postgresServer.Resource.Name} is implemented by {implementation}, which is not a container.");
            }
        }

        var postgresContainer = new PostgresContainerResource(postgresServer.Resource.Name, postgresServer.Resource.Password);
        var impl = postgresServer.ApplicationBuilder.AddResource(postgresContainer)
                      .WithManifestPublishingCallback(context => WritePostgresContainerResourceToManifest(context, postgresContainer))
                      .WithAnnotation(new EndpointAnnotation(ProtocolType.Tcp, containerPort: 5432)) // Internal port is always 5432.
                      .WithAnnotation(new ContainerImageAnnotation { Image = "postgres", Tag = "latest" })
                      .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "scram-sha-256")
                      .WithEnvironment("POSTGRES_INITDB_ARGS", "--auth-host=scram-sha-256 --auth-local=scram-sha-256")
                      .WithEnvironment(context =>
                      {
                          if (context.PublisherName == "manifest")
                          {
                              context.EnvironmentVariables.Add(PasswordEnvVarName, $"{{{postgresContainer.Name}.inputs.password}}");
                          }
                          else
                          {
                              context.EnvironmentVariables.Add(PasswordEnvVarName, postgresContainer.Password);
                          }
                      });
        abstractResource.Implementation = impl.Resource;
        return impl;
    }

    /// <summary>
    /// Adds a PostgreSQL resource to the application model. A container is used for local development.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostgresContainerResource}"/>.</returns>
    public static IResourceBuilder<PostgresServerResource> AddPostgres(this IDistributedApplicationBuilder builder, string name)
    {
        var password = Guid.NewGuid().ToString("N");
        var postgresServer = new PostgresServerResource(name, password);
        return builder.AddResource(postgresServer)
            .WithBuildCallback((builder, ctx) =>
            {
                // If an implementation has been specified already, then we don't need to do anything.
                if (builder.HasImplementationResource())
                {
                    if (ctx.OperationType == OperationContext.PublishManifest)
                    {
                        builder.ExcludeFromManifest();
                    }

                    return;
                }

                // Run default actions, depending on what the model is being used for.
                switch (ctx.OperationType)
                {
                    case OperationContext.Run:
                        builder.AsContainer();
                        break;
                    case OperationContext.PublishManifest:
                        builder.WithManifestPublishingCallback(WritePostgresContainerToManifest);
                        break;
                }
            })
          .WithEnvironment(PasswordEnvVarName, () => postgresServer.Password);
    }

    /// <summary>
    /// Adds a PostgreSQL database to the application model.
    /// </summary>
    /// <param name="builder">The PostgreSQL server resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostgresDatabaseResource}"/>.</returns>
    public static IResourceBuilder<PostgresDatabaseResource> AddDatabase(this IResourceBuilder<IPostgresServiceResource> builder, string name)
    {
        var postgresDatabase = new PostgresDatabaseResource(name, builder.Resource);
        return builder.ApplicationBuilder.AddResource(postgresDatabase)
                                         .WithManifestPublishingCallback(context => WritePostgresDatabaseToManifest(context, postgresDatabase));
    }

    /// <summary>
    /// Adds a pgAdmin 4 administration and development platform for PostgreSQL to the application model.
    /// </summary>
    /// <param name="builder">The PostgreSQL server resource builder.</param>
    /// <param name="hostPort">The host port for the application ui.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostgresContainerResource}"/>.</returns>
    public static IResourceBuilder<T> WithPgAdmin<T>(this IResourceBuilder<T> builder, int? hostPort = null, string? containerName = null) where T : IPostgresServiceResource
    {
        builder.WithBuildCallback(context =>
        {
            // PgAdmin is only relevant when running the application, not when publishing the manifest.
            if (context.OperationType != OperationContext.Run)
            {
                return;
            }

            // If Postgres isn't implemented as a container, pgAdmin is not relevant.
            if (builder.GetImplementationResource() is not ContainerResource)
            {
                return;
            }

            // Only one pgAdmin container is allowed per application.
            if (builder.ApplicationBuilder.Resources.OfType<PgAdminContainerResource>().Any())
            {
                return;
            }

            builder.ApplicationBuilder.Services.TryAddLifecycleHook<PgAdminConfigWriterHook>();

            containerName ??= $"{builder.Resource.Name}-pgadmin";

            var pgAdminContainer = new PgAdminContainerResource(containerName);
            builder.ApplicationBuilder.AddResource(pgAdminContainer)
                                      .WithAnnotation(new ContainerImageAnnotation { Image = "dpage/pgadmin4", Tag = "latest" })
                                      .WithEndpoint(containerPort: 80, hostPort: hostPort, scheme: "http", name: containerName)
                                      .WithEnvironment(SetPgAdminEnviromentVariables)
                                      .WithVolumeMount(Path.GetTempFileName(), "/pgadmin4/servers.json")
                                      .ExcludeFromManifest();
        });

        return builder;
    }

    private static void SetPgAdminEnviromentVariables(EnvironmentCallbackContext context)
    {
        // Disables pgAdmin authentication.
        context.EnvironmentVariables.Add("PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED", "False");
        context.EnvironmentVariables.Add("PGADMIN_CONFIG_SERVER_MODE", "False");

        // You need to define the PGADMIN_DEFAULT_EMAIL and PGADMIN_DEFAULT_PASSWORD or PGADMIN_DEFAULT_PASSWORD_FILE environment variables.
        context.EnvironmentVariables.Add("PGADMIN_DEFAULT_EMAIL", "admin@domain.com");
        context.EnvironmentVariables.Add("PGADMIN_DEFAULT_PASSWORD", "admin");
    }

    private static void WritePostgresContainerToManifest(ManifestPublishingContext context)
    {
        context.Writer.WriteString("type", "postgres.server.v0");
    }

    private static void WritePostgresDatabaseToManifest(ManifestPublishingContext context, PostgresDatabaseResource postgresDatabase)
    {
        context.Writer.WriteString("type", "postgres.database.v0");
        context.Writer.WriteString("parent", postgresDatabase.Parent.Name);
    }

    private static void WritePostgresContainerResourceToManifest(ManifestPublishingContext context, PostgresContainerResource resource)
    {
        context.WriteContainer(resource);
        context.Writer.WriteString(                     // "connectionString": "...",
            "connectionString",
            $"Host={{{resource.Name}.bindings.tcp.host}};Port={{{resource.Name}.bindings.tcp.port}};Username=postgres;Password={{{resource.Name}.inputs.password}};");
        context.Writer.WriteStartObject("inputs");      // "inputs": {
        context.Writer.WriteStartObject("password");    //   "password": {
        context.Writer.WriteString("type", "string");   //     "type": "string",
        context.Writer.WriteBoolean("secret", true);    //     "secret": true,
        context.Writer.WriteStartObject("default");     //     "default": {
        context.Writer.WriteStartObject("generate");    //       "generate": {
        context.Writer.WriteNumber("minLength", 10);    //         "minLength": 10,
        context.Writer.WriteEndObject();                //       }
        context.Writer.WriteEndObject();                //     }
        context.Writer.WriteEndObject();                //   }
        context.Writer.WriteEndObject();                // }
    }
}
