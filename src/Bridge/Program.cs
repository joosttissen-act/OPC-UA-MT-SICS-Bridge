using Bridge;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<OpcUaOptions>(
    builder.Configuration.GetSection(OpcUaOptions.SectionName));
builder.Services.Configure<MtSicsServerOptions>(
    builder.Configuration.GetSection(MtSicsServerOptions.SectionName));

builder.Services.AddSingleton<IOpcUaScaleClient, OpcUaScaleClient>();
builder.Services.AddSingleton<CommandTranslator>();
builder.Services.AddSingleton<MtSicsTcpServer>();

builder.Services.AddHostedService<ScaleService>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "OPC UA MT-SICS Bridge";
});

var host = builder.Build();
host.Run();
