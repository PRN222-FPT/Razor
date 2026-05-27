using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();

    Task<CategoryDto?> GetByIdAsync(int id);

    Task<CategoryDto> CreateAsync(CategoryCreateDto dto);

    Task<bool> UpdateAsync(int id, CategoryUpdateDto dto);

    Task<bool> DeleteAsync(int id);
}
