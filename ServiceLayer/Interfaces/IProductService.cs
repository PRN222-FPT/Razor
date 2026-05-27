using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync();

    Task<ProductDto?> GetByIdAsync(int id);

    Task<ProductDto> CreateAsync(ProductCreateDto dto);

    Task<bool> UpdateAsync(int id, ProductUpdateDto dto);

    Task<bool> DeleteAsync(int id);

    Task<IEnumerable<ProductDto>> GetByCategoryAsync(int categoryId);
}
