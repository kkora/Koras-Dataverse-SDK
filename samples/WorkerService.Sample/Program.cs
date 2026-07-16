// Worker Service sample: a background job that pages through Dataverse rows with
// QueryAllAsync and applies bulk updates with an atomic batch. See README.md for setup.
using Koras.Dataverse.Samples.WorkerService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDataverse(options =>
{
    IConfiguration config = builder.Configuration;
    options.EnvironmentUrl = new Uri(config["Dataverse:EnvironmentUrl"]
        ?? throw new InvalidOperationException("Configure Dataverse:EnvironmentUrl."));
    options.Authentication.UseDefault();
    options.Retry.MaxRetries = 5; // batch jobs tolerate longer throttling waits
});

builder.Services.AddHostedService<AccountSweepWorker>();

builder.Build().Run();
