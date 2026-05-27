using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Categories;

public class IndexModel : PageModel
{
    private readonly ICategoryService _categoryService;

    public IndexModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public IEnumerable<CategoryIndexViewModel> Categories { get; private set; } = new List<CategoryIndexViewModel>();

    public async Task OnGetAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        Categories = categories.Select(c => new CategoryIndexViewModel
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            ProductCount = c.ProductCount
        });
    }
}
