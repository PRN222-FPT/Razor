using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        var products = await _unitOfWork.Products
            .Query()
            .Include(p => p.Category)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(MapToDto);
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var product = await _unitOfWork.Products
            .Query()
            .Include(p => p.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == id);

        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto)
    {
        var entity = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            CategoryId = dto.CategoryId
        };

        await _unitOfWork.Products.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _unitOfWork.Products
            .Query()
            .Include(p => p.Category)
            .FirstAsync(p => p.ProductId == entity.ProductId);

        return MapToDto(created);
    }

    public async Task<bool> UpdateAsync(int id, ProductUpdateDto dto)
    {
        var entity = await _unitOfWork.Products.GetByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Price = dto.Price;
        entity.StockQuantity = dto.StockQuantity;
        entity.CategoryId = dto.CategoryId;

        _unitOfWork.Products.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _unitOfWork.Products.GetByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        _unitOfWork.Products.Delete(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<ProductDto>> GetByCategoryAsync(int categoryId)
    {
        var products = await _unitOfWork.Products
            .Query()
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(MapToDto);
    }

    private static ProductDto MapToDto(Product product) => new(
        product.ProductId,
        product.Name,
        product.Description,
        product.Price,
        product.StockQuantity,
        product.CategoryId,
        product.Category.Name);
}
