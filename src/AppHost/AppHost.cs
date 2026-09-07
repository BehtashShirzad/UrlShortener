var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres").WithImage("postgres:17")
    .WithDataVolume();

var database = postgres
    .AddDatabase("shortlinks");

var redis = builder
    .AddRedis("redis");

var migrations = builder
    .AddProject<Projects.Migrations>("migrations")
    .WithReference(database)
    .WaitFor(database);

var api = builder
    .AddProject<Projects.Api>("api")
    .WithReference(database)
    .WithReference(redis)
    .WaitForCompletion(migrations)
    .WaitFor(redis);

builder.Build().Run();