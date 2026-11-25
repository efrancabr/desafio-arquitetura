using FluxoCaixa.Application;
using FluxoCaixa.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Worker de consolidação: consome eventos de lançamento e atualiza o read model.
builder.Services.AddConsolidadoApplication();
builder.Services.AddConsolidadoInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
