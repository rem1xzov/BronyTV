using BronyTV.Service;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeasonController : ControllerBase
{
    private readonly ISeasonService _seasonService;

    public SeasonController(ISeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var seasons = await _seasonService.GetAllSeasonsAsync();
        return Ok(seasons);
    }
}