using Microsoft.AspNetCore.Mvc.RazorPages;
using Razor.ViewModels;
using ServiceLayer.Interfaces;

namespace Razor.Pages.Products;

public class IndexModel : PageModel
{
    private readonly IProductService _productService;

    public IndexModel(IProductService productService)
    {
        _productService = productService;
    }

    public IEnumerable<ProductIndexViewModel> Products { get; private set; } = new List<ProductIndexViewModel>();

    public async Task OnGetAsync()
    {
        var products = await _productService.GetAllAsync();
        Products = products.Select(p => new ProductIndexViewModel
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            CategoryName = p.CategoryName
        });
    }
}
