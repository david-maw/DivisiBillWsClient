namespace DivisiBillWsClient.Models;

/// <summary>
/// Represents a product price item.
/// </summary>
public class PriceItem
{
    /// <summary>
    /// Display name of the product.
    /// </summary>
    public required string ProductName { get; set; }

    /// <summary>
    /// The price of the product.
    /// </summary>
    public string? Price { get; set; }
}
