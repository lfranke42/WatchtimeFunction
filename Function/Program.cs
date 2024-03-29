﻿using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

// This defines the host that runs in the azure function host process
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseNewtonsoftJson())
    // services for dependecy injection
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddTableServiceClient(hostContext.Configuration
                .GetWebJobsConnectionString("AzureWebJobsStorage"));
        });
    })
    .Build();

host.Run();