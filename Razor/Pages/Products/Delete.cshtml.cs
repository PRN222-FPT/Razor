using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Products;

public class DeleteModel : PageModel
{
    private readonly IProductService _productService;

    public DeleteModel(IProductService productService)
    {
        _productService = productService;
    }

    [BindProperty]
    public ProductDeleteViewModel Product { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = new ProductDeleteViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryName = product.CategoryName
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id != Product.ProductId)
        {
            return BadRequest();
        }

        var success = await _productService.DeleteAsync(id);

        return success ? RedirectToPage("Index") : NotFound();
    }
}
