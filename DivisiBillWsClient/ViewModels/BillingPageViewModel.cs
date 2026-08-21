using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DivisiBillWsClient.InAppBilling;
using DivisiBillWsClient.Models;
using DivisiBillWsClient.Services;
using System.Collections.ObjectModel;

namespace DivisiBillWsClient.ViewModels;

/// <summary>
/// ViewModel for the Billing page. Handles loading and displaying in-app billing purchases.
/// </summary>
public partial class BillingPageViewModel : ObservableObject
{
    /// <summary>
    /// Collection of billing items (subscriptions and licenses).
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<BillingItem> BillingItems { get; set; } = [];

    /// <summary>
    /// The currently selected billing item.
    /// </summary>
    [ObservableProperty]
    public partial BillingItem? SelectedBillingItem { get; set; }

    /// <summary>
    /// Collection of product prices.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<PriceItem> PriceItems { get; set; } = [
        new PriceItem { ProductName = Billing.ProSubscriptionId, Price = null },
        new PriceItem { ProductName = Billing.OldProProductId, Price = null },
        new PriceItem { ProductName = Billing.OcrLicenseProductId, Price = null }];

    /// <summary>
    /// Status message for the last operation.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Result of the verification call.
    /// </summary>
    [ObservableProperty]
    public partial string? VerificationResult { get; set; }

    /// <summary>
    /// Indicates whether the view is currently loading purchases.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Loads both subscriptions and licenses asynchronously.
    /// </summary>
    [RelayCommand]
    private async Task LoadBillingItems()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading subscriptions and licenses...";
            BillingItems?.Clear();
            SelectedBillingItem = null;
            VerificationResult = null;

            // Load subscriptions
            var (subscriptionStatus, subscriptionList) = await Billing.GetInAppBillingPurchaseListAsync(isSubscription: true);

            // Load licenses (one-time purchases)
            var (licenseStatus, licenseList) = await Billing.GetInAppBillingPurchaseListAsync(isSubscription: false);

            var totalCount = 0;

            if (subscriptionStatus == Billing.BillingStatusType.ok && subscriptionList != null)
            {
                foreach (var purchase in subscriptionList)
                {
                    BillingItems?.Add(new BillingItem
                    {
                        Purchase = purchase,
                        Type = BillingItemType.Subscription
                    });
                    totalCount++;
                }
            }

            if (licenseStatus == Billing.BillingStatusType.ok && licenseList != null)
            {
                foreach (var purchase in licenseList)
                {
                    BillingItems?.Add(new BillingItem
                    {
                        Purchase = purchase,
                        Type = BillingItemType.License
                    });
                    totalCount++;
                }
            }

            if (totalCount > 0)
            {
                StatusMessage = $"Loaded {totalCount} item(s) - {BillingItems?.Count(x => x.Type == BillingItemType.Subscription)} subscription(s), {BillingItems?.Count(x => x.Type == BillingItemType.License)} license(s)";
            }
            else
            {
                StatusMessage = $"Failed to load items: Subscriptions={subscriptionStatus}, Licenses={licenseStatus}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Loads the prices for the three product types asynchronously.
    /// </summary>
    [RelayCommand]
    private async Task LoadPrices()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading prices...";

            foreach (var priceItem in PriceItems)
            {
                var itemType = priceItem.ProductName == Billing.ProSubscriptionId ? ItemType.Subscription : ItemType.InAppPurchase;
                priceItem.Price = await Billing.GetItemPriceAsync(priceItem.ProductName, itemType);
            }

            // Force a reload of PriceItems
            var temp = PriceItems;
            PriceItems = [];
            PriceItems = temp;

            if (PriceItems.Any(p => p.Price != null))
            {
                StatusMessage = "Prices loaded successfully";
            }
            else
            {
                StatusMessage = "Failed to load prices";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading prices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Verifies the selected billing item purchase asynchronously.
    /// </summary>
    [RelayCommand]
    private async Task VerifyPurchase()
    {
        if (SelectedBillingItem?.Purchase is null)
        {
            StatusMessage = "Please select a billing item to verify.";
            VerificationResult = null;
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Verifying {SelectedBillingItem.Purchase.ProductId}...";
            VerificationResult = "Verifying...";

            var isVerified = await CallWs.TryVerifyPurchase(SelectedBillingItem.Purchase);

            VerificationResult = isVerified ? "✓ Verified" : "✗ Failed";
            StatusMessage = isVerified
                ? $"✓ {SelectedBillingItem.Purchase.ProductId} verified successfully"
                : $"✗ Verification failed for {SelectedBillingItem.Purchase.ProductId}";
        }
        catch (Exception ex)
        {
            VerificationResult = $"✗ Error";
            StatusMessage = $"Error verifying purchase: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
