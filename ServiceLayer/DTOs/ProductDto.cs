namespace ServiceLayer.DTOs;

public record ProductDto(
    int ProductId,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    int CategoryId,
    string CategoryName);

public record ProductCreateDto(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    int CategoryId);

public record ProductUpdateDto(
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    int CategoryId);
