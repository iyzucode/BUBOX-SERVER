using Bubox.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bubox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeneralController(IGeneralService generalService) : ControllerBase
{
    [HttpGet("GetProvinces")]
    public async Task<IActionResult> GetProvinces()
    {
        var provinces = await generalService.GetProvincesAsync();
        return Ok(provinces);
    }

    [HttpGet("GetCities")]
    public async Task<IActionResult> GetCities([FromQuery] string provinceCode)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return BadRequest(new { message = "Parameter 'provinceCode' wajib disertakan." });
        }

        var cities = await generalService.GetCitiesAsync(provinceCode);
        return Ok(cities);
    }

    [HttpGet("GetDistricts")]
    public async Task<IActionResult> GetDistricts([FromQuery] string cityCode)
    {
        if (string.IsNullOrWhiteSpace(cityCode))
        {
            return BadRequest(new { message = "Parameter 'cityCode' wajib disertakan." });
        }

        var districts = await generalService.GetDistrictsAsync(cityCode);
        return Ok(districts);
    }

    [HttpGet("GetVillages")]
    public async Task<IActionResult> GetVillages([FromQuery] string districtCode)
    {
        if (string.IsNullOrWhiteSpace(districtCode))
        {
            return BadRequest(new { message = "Parameter 'districtCode' wajib disertakan." });
        }

        var villages = await generalService.GetVillagesAsync(districtCode);
        return Ok(villages);
    }
}
