using System.Net.Http.Headers;
using Ecommerce.DAL;
using Ecommerce.Domain.Models;
using Ecommerce.Localization;
using Ecommerce.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Localization;
using StringWithQualityHeaderValue = Microsoft.Net.Http.Headers.StringWithQualityHeaderValue;

namespace Ecommerce.Contollers;

[ApiController]
[ApiVersion("1")]
[Route("v{apiVer:apiVersion}/[controller]")]
public class CategoriesController : ControllerBase
{
    private AppDbContext _db;
    private IStringLocalizer<ErrorMessages> _localizer;
    private IValidator<CreateCategoryDto> _createCategoryDtoValidator;

    public CategoriesController(AppDbContext db,
        IStringLocalizer<ErrorMessages> localizer,
        IValidator<CreateCategoryDto> createCategoryDtoValidator)
    {
        _db = db;
        _localizer = localizer;
        _createCategoryDtoValidator = createCategoryDtoValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Language lang = Language.Ua)
    {
        var categories = _db.CategoryNames
            .Include(cn => cn.Category)
            .Where(c => c.Language == lang && c.Category.ParentCategoryId == null)
            .Select(cn => CategoriesDto.MapFromCategoryName(cn))
            .ToList();

        GetCategories(categories, lang);

        return Ok(categories);
    }

    private List<CategoriesDto> GetCategories(List<CategoriesDto> categories, Language lang)
    {
        foreach (var category in categories)
        {
            var sub = _db.CategoryNames.Include(c => c.Category)
                .Where(cn => cn.Category.ParentCategoryId == category.CategoryId &&
                             cn.Language == lang)
                .Select(cn => CategoriesDto.MapFromCategoryName(cn)).ToList();
            
            if (sub.Count == 0)
                return categories;
            category.Childrens?.AddRange(GetCategories(sub, lang));
        }
        return categories;
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto categoryDto)
    {
        var result = await _createCategoryDtoValidator.ValidateAsync(categoryDto);

        if (!result.IsValid)
            return BadRequest(new ErrorMessage(result.Errors
                .Select(e => e.ErrorMessage)));

        foreach (var categoryName in categoryDto.Names)
        {
            if (await _db.CategoryNames.AnyAsync(c => c.Name == categoryName.Name
                                                      && c.Language == categoryName.Language))
                return Conflict(new ErrorMessage(_localizer["Category"] +
                                                 " " +
                                                 _localizer["AlreadyExists"]));
        }

        var categoryNames = categoryDto.Names
            .Select(n => n.ToCategoryName());

        string filename = Guid.NewGuid() + "_" + categoryDto.Image.Filename;

        var category = new Category
        {
            Image = new CategoryImage
            {
                Filename = filename
            },
            Names = new List<CategoryName>(categoryNames)
        };

        await _db.Categories.AddAsync(category);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e)
        {
            return StatusCode(500, new ErrorMessage(_localizer["InternalError"]));
        }

        var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/", filename);

        await categoryDto.Image.SaveToFile(imagePath);

        // TODO: url to created category
        return Created("", category);
    }
}