namespace Thermion.Controllers;

using Microsoft.AspNetCore.Mvc;
using Services;

[ApiController]
[Route("api")]
public class ControlController(IDockerService docker) : ControllerBase
{
    [HttpGet("state")]
    public async Task<IActionResult> GetState()
    {
        var info = await docker.GetDockerInfoAsync();
        return Ok(new
        {
            info.ServerVersion,
            info.OperatingSystem,
            info.ID,
            info.Name
        });
    }

    [HttpPost("scale")]
    public async Task<IActionResult> Scale([FromBody] ScaleRequest request)
    {
        var result = await docker.ScaleServiceAsync("coturn", request.Replicas);
        return result ? Ok() : StatusCode(500, "Failed to scale");
    }
}

public class ScaleRequest
{
    public ulong Replicas { get; set; }
}