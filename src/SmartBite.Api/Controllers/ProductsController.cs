using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(SmartBiteDbContext db) : ControllerBase
{
    /// <summary>Products of the user, optionally filtered by part of the name and by category.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ProductResponse>> GetAll(
        string? search,
        ProductCategory? category
    )
    {
        var userId = User.GetId();
        var query = db.Products.Where(p => p.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }
        if (category is not null)
        {
            query = query.Where(p => p.Category == category);
        }

        return await query.OrderBy(p => p.Name).Select(p => ToResponse(p)).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Get(int id)
    {
        var product = await FindAsync(id);
        return product is null ? NotFoundProblem() : ToResponse(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(ProductRequest request)
    {
        var product = new Product { UserId = User.GetId() };
        Apply(product, request);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = product.Id }, ToResponse(product));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(int id, ProductRequest request)
    {
        var product = await FindAsync(id);
        if (product is null)
        {
            return NotFoundProblem();
        }

        Apply(product, request);
        await db.SaveChangesAsync();
        return ToResponse(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await FindAsync(id);
        if (product is null)
        {
            return NotFoundProblem();
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<Product?> FindAsync(int id)
    {
        var userId = User.GetId();
        return db.Products.SingleOrDefaultAsync(p => p.Id == id && p.UserId == userId);
    }

    private ObjectResult NotFoundProblem() => Problem("Продукт не знайдено", statusCode: 404);

    private static void Apply(Product product, ProductRequest request)
    {
        product.Name = request.Name.Trim();
        product.Quantity = request.Quantity;
        product.Unit = request.Unit;
        product.Category = request.Category;
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Quantity, p.Unit, p.Category);
}
