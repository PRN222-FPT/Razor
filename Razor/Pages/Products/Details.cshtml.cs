using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Products;

public class DetailsModel : PageModel
{
    private readonly IProductService _productService;

    public DetailsModel(IProductService productService)
    {
        _productService = productService;
    }

    public ProductDetailsViewModel Product { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = new ProductDetailsViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName
        };

        return Page();
    }
}
