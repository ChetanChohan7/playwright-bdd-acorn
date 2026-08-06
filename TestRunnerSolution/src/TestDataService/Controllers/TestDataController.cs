using Microsoft.AspNetCore.Mvc;
using TestDataService.Services;

namespace TestDataService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestDataController : ControllerBase
{
    private readonly TestRunnerQueryService _testRunnerQueryService;

    public TestDataController(TestRunnerQueryService testRunnerQueryService)
    {
        _testRunnerQueryService = testRunnerQueryService;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _testRunnerQueryService.GetTableNamesAsync();
        return Ok(tables);
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> GetRows(string tableName, [FromQuery] int limit = 25)
    {
        try
        {
            var result = await _testRunnerQueryService.GetRowsAsync(tableName, limit);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
