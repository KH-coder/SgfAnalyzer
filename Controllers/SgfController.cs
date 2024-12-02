using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SgfController : ControllerBase
{
    private readonly ISgfService _sgfService;
    private readonly ILogger<SgfController> _logger;

    public SgfController(ISgfService sgfService, ILogger<SgfController> logger)
    {
        _sgfService = sgfService;
        _logger = logger;
    }

    [HttpPost("region")]
    public async Task<IActionResult> GenerateRegionImage([FromForm] SgfRequest request)
    {
        try
        {
            if (request.SgfFile == null || request.SgfFile.Length == 0)
            {
                return BadRequest("SGF file is required");
            }

            if (string.IsNullOrEmpty(request.RegionName))
            {
                return BadRequest("Region name is required");
            }

            // 讀取和處理 SGF 內容
            string sgfContent = await ProcessSgfFile(request.SgfFile);

            var imageBytes = await _sgfService.GenerateRegionImage(sgfContent, request.RegionName);
            var base64Image = Convert.ToBase64String(imageBytes);

            return Ok(new SgfResponse
            {
                Message = "Image generated successfully",
                ImageBase64 = base64Image
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request");
            return BadRequest(new SgfResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SGF file");
            return StatusCode(500, new SgfResponse { Message = "Internal server error" });
        }
    }

    [HttpPost("all-regions")]
    public async Task<IActionResult> GenerateAllRegionImages([FromForm] SgfRequest request)
    {
        try
        {
            if (request.SgfFile == null || request.SgfFile.Length == 0)
            {
                return BadRequest("SGF file is required");
            }

            // 使用相同的處理方法
            string sgfContent = await ProcessSgfFile(request.SgfFile);

            var imagesDict = await _sgfService.GenerateAllRegionImages(sgfContent);
            var response = imagesDict.ToDictionary(
                kvp => kvp.Key,
                kvp => Convert.ToBase64String(kvp.Value)
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SGF file");
            return StatusCode(500, new SgfResponse { Message = "Internal server error" });
        }
    }

    private async Task<string> ProcessSgfFile(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        string sgfContent = await reader.ReadToEndAsync();
        
        // Trim 空白和移除無關字符
        sgfContent = sgfContent.Trim();
        
        // 移除換行符和多餘空格
        sgfContent = sgfContent.Replace("\r", "")
                            .Replace("\n", "")
                            .Replace("\t", "");
        
        // 確保 SGF 格式正確
        if (!sgfContent.StartsWith("(;") || !sgfContent.EndsWith(")"))
        {
            throw new ArgumentException("Invalid SGF format");
        }
        
        // 如果需要，還可以加入其他 SGF 內容驗證

        return sgfContent;
    }
}