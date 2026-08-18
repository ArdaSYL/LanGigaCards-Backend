using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabGrid.DTOs;
using VocabGrid.Entities;
using VocabGrid.Interfaces;

namespace VocabGrid.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoriesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// List categories. Optional search: ?q=food
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories([FromQuery] string? q = null)
    {
        // Süzme ve sıralama veritabanında. Eski hali önce tüm katalogu
        // GetAllAsync ile çekip bellekte filtreliyordu; katalog bugün 15
        // satır olduğu için görünür bir maliyeti yoktu ama aynı kalıp her
        // okuma yolunda tekrarlanıyor. LIKE, SQL Server'ın harf
        // büyüklüğüne duyarsız harmanlamasıyla zaten büyük-küçük harf
        // ayrımı yapmadan eşleşir.
        var query = _unitOfWork.Repository<Category>().Query();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(category =>
                EF.Functions.Like(category.Name, pattern) ||
                (category.Description != null && EF.Functions.Like(category.Description, pattern)));
        }

        return Ok(await query
            .OrderBy(category => category.Id)
            .Select(category => new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IconName = category.IconName,
                ColorHex = category.ColorHex
            })
            .ToListAsync());
    }
}
