using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Categories;

public class CreateModel : PageModel
{
    private readonly ICategoryService _categoryService;

    public CreateModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [BindProperty]
    public CategoryCreateViewModel Category { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var dto = new CategoryCreateDto(Category.Name, Category.Description);
        await _categoryService.CreateAsync(dto);

        return RedirectToPage("Index");
    }
}
