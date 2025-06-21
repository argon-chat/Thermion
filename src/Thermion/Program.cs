using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Newtonsoft.Json;
using Thermion.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ThermionConfig>(x =>
{
    var base64 = Environment.GetEnvironmentVariable("THERMION_CONFIG");
    if (string.IsNullOrWhiteSpace(base64))
        throw new Exception("Missing THERMION_CONFIG environment variable");

    var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    return JsonConvert.DeserializeObject<ThermionConfig>(json)!;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDockerService, DockerService>();

var app = builder.Build();

app.UseRouting();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();