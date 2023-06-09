using Ecommerce.Domain.Models;

namespace Ecommerce.Models;

public class CategoriesDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; }
    public string ImageFilename { get; set; }
    public List<CategoriesDto> Children { get; set; } = new();

    public static CategoriesDto MapFromCategoryName(CategoryName categoryName)
    {
        return new CategoriesDto
        {
            CategoryId = categoryName.CategoryId,
            Name = categoryName.Name,
            ImageFilename = categoryName.Category.Image?.Filename ?? ""
        };
    }
}