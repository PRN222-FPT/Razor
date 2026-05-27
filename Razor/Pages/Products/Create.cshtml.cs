using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Razor.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Products;

public class CreateModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public CreateModel(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    [BindProperty]
    public ProductCreateViewModel Product { get; set; } = new();

    public async Task OnGetAsync()
    {
        Product.Categories = await GetCategorySelectListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _categoryService.GetByIdAsync(Product.CategoryId) is null)
        {
            ModelState.AddModelError("Product.CategoryId", "Selected category does not exist.");
        }

        if (!ModelState.IsValid)
        {
            Product.Categories = await GetCategorySelectListAsync();
            return Page();
        }

        var dto = new ProductCreateDto(
            Product.Name,
            Product.Description,
            Product.Price,
            Product.StockQuantity,
            Product.CategoryId);

        await _productService.CreateAsync(dto);

        return RedirectToPage("Index");
    }

    private async Task<SelectList> GetCategorySelectListAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        return new SelectList(
            categories.Select(c => new { c.CategoryId, c.Name }),
            "CategoryId",
            "Name");
    }
}
