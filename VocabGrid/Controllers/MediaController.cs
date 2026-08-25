using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VocabGrid.Services;

namespace VocabGrid.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".mp3", ".wav", ".m4a", ".ogg"
    };

    private readonly IWebHostEnvironment _environment;

    public MediaController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (TryGetUserId() is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest("Unsupported file type.");
        }

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var uploadsDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var normalizedExtension = extension.ToLowerInvariant();
        long storedSizeBytes;
        string storedContentType;

        if (ImageOptimizer.CanOptimize(normalizedExtension))
        {
            // Optimization always outputs WebP -- see ImageOptimizer's doc
            // comment for why -- so the stored file's extension can differ
            // from what was uploaded even though the original upload was
            // already a supported image type.
            normalizedExtension = ".webp";
            storedContentType = "image/webp";

            await using var upload = file.OpenReadStream();
            using var optimized = await ImageOptimizer.OptimizeAsync(upload);
            storedSizeBytes = optimized.Length;

            var optimizedFileName = $"{Guid.NewGuid():N}{normalizedExtension}";
            var optimizedPath = Path.Combine(uploadsDir, optimizedFileName);
            await using (var destination = System.IO.File.Create(optimizedPath))
            {
                await optimized.CopyToAsync(destination);
            }

            return Ok(new
            {
                Url = $"/uploads/{optimizedFileName}",
                FileName = optimizedFileName,
                ContentType = storedContentType,
                SizeBytes = storedSizeBytes,
                OriginalSizeBytes = file.Length
            });
        }

        // Animated GIFs and audio files: stored as uploaded, unoptimized --
        // see ImageOptimizer's doc comment for why GIFs specifically are
        // excluded (re-encoding would destroy the animation).
        var fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
        var physicalPath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new
        {
            Url = $"/uploads/{fileName}",
            FileName = fileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length
        });
    }

    private int? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
