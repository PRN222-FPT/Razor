using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Razor.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Products;

public class EditModel : PageModel
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public EditModel(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    [BindProperty]
    public ProductEditViewModel Product { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = new ProductEditViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            Categories = await GetCategorySelectListAsync()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id != Product.ProductId)
        {
            return BadRequest();
        }

        if (await _categoryService.GetByIdAsync(Product.CategoryId) is null)
        {
            ModelState.AddModelError("Product.CategoryId", "Selected category does not exist.");
        }

        if (!ModelState.IsValid)
        {
            Product.Categories = await GetCategorySelectListAsync();
            return Page();
        }

        var dto = new ProductUpdateDto(
            Product.Name,
            Product.Description,
            Product.Price,
            Product.StockQuantity,
            Product.CategoryId);

        var success = await _productService.UpdateAsync(id, dto);

        return success ? RedirectToPage("Index") : NotFound();
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
