using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Categories;

public class DetailsModel : PageModel
{
    private readonly ICategoryService _categoryService;

    public DetailsModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public CategoryDetailsViewModel Category { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        Category = new CategoryDetailsViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            ProductCount = category.ProductCount
        };

        return Page();
    }
}
