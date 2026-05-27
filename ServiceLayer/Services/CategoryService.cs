using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var categories = await _unitOfWork.Categories
            .Query()
            .Include(c => c.Products)
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => new CategoryDto(
            c.CategoryId,
            c.Name,
            c.Description,
            c.Products.Count));
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var category = await _unitOfWork.Categories
            .Query()
            .Include(c => c.Products)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return null;
        }

        return new CategoryDto(
            category.CategoryId,
            category.Name,
            category.Description,
            category.Products.Count);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
    {
        var entity = new Category
        {
            Name = dto.Name,
            Description = dto.Description
        };

        await _unitOfWork.Categories.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return new CategoryDto(entity.CategoryId, entity.Name, entity.Description, 0);
    }

    public async Task<bool> UpdateAsync(int id, CategoryUpdateDto dto)
    {
        var entity = await _unitOfWork.Categories.GetByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;

        _unitOfWork.Categories.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _unitOfWork.Categories.GetByIdAsync(id);
        if (entity is null)
        {
            return false;
        }

        var hasProducts = await _unitOfWork.Products
            .Query()
            .AnyAsync(p => p.CategoryId == id);

        if (hasProducts)
        {
            return false;
        }

        _unitOfWork.Categories.Delete(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}
