using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Categories;

public class DeleteModel : PageModel
{
    private readonly ICategoryService _categoryService;

    public DeleteModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [BindProperty]
    public CategoryDeleteViewModel Category { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        Category = new CategoryDeleteViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            ProductCount = category.ProductCount
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id != Category.CategoryId)
        {
            return BadRequest();
        }

        var success = await _categoryService.DeleteAsync(id);
        if (!success)
        {
            TempData["Error"] = "Cannot delete category that still has products.";
            return RedirectToPage("Delete", new { id });
        }

        return RedirectToPage("Index");
    }
}
