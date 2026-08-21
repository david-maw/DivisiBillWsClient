using DivisiBillWsClient.InAppBilling;

namespace DivisiBillWsClient.Models;

/// <summary>
/// Represents a billing item (subscription or license) with type information.
/// </summary>
public class BillingItem
{
    /// <summary>
    /// The underlying purchase/subscription data.
    /// </summary>
    public required InAppBillingPurchase Purchase { get; set; }

    /// <summary>
    /// The type of billing item (Subscription or License).
    /// </summary>
    public BillingItemType Type { get; set; }

    /// <summary>
    /// User-friendly type display string.
    /// </summary>
    public string TypeDisplay => Type switch
    {
        BillingItemType.Subscription => "📅 Subscription",
        BillingItemType.License => "🔑 License",
        _ => "Unknown"
    };
    public bool IsSubscription => Type == BillingItemType.Subscription;
}

/// <summary>
/// Defines the type of billing item.
/// </summary>
public enum BillingItemType
{
    /// <summary>
    /// A recurring subscription.
    /// </summary>
    Subscription,

    /// <summary>
    /// A one-time license purchase.
    /// </summary>
    License
}
