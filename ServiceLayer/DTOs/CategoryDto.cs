namespace ServiceLayer.DTOs;

public record CategoryDto(int CategoryId, string Name, string? Description, int ProductCount);

public record CategoryCreateDto(string Name, string? Description);

public record CategoryUpdateDto(string Name, string? Description);
