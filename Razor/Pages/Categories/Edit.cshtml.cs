using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Categories;

public class EditModel : PageModel
{
    private readonly ICategoryService _categoryService;

    public EditModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [BindProperty]
    public CategoryEditViewModel Category { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        Category = new CategoryEditViewModel
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Description = category.Description
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id != Category.CategoryId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var dto = new CategoryUpdateDto(Category.Name, Category.Description);
        var success = await _categoryService.UpdateAsync(id, dto);

        return success ? RedirectToPage("Index") : NotFound();
    }
}
