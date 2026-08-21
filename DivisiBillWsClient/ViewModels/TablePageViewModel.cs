using Azure;
using Azure.Data.Tables;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DivisiBillWsClient.Generated;
using System.Collections.ObjectModel;

namespace DivisiBillWsClient.ViewModels;

/// <summary>
/// Options for data environment selection
/// </summary>
public enum EnvironmentOption
{
    Development,
    Alternate,
    Production
}

/// <summary>
/// Data model for Meal table entries
/// </summary>
public class MealData
{
    public string UserId { get; set; } = "";
    public string PartitionKey { get; set; } = "";
    public DateTime? LatestTimestamp { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Data model for License table entries
/// </summary>
public class LicenseData
{
    public string UserId { get; set; } = "";
    public string ObfuscatedAccountId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public DateTime? LatestTimeUsed { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// ViewModel for the Table page. Handles loading and displaying Azure Table data.
/// </summary>
public partial class TablePageViewModel : ObservableObject
{
    // Production connection string and prefix for table names (base64 encoded):
    static readonly string productionConnectionString = DecodeConnectionString(BuildInfo.DivisiBillWsProductionConnectionStringB64);
    const string productionPrefix = "DivisiBill";

    // Alternate connection string and prefix for table names (base64 encoded):
    static readonly string alternateConnectionString = DecodeConnectionString(BuildInfo.DivisiBillWsAlternateConnectionStringB64);
    const string alternatePrefix = "DivisiBill";

    // Development tables in Azurite (local emulator) connection string and prefix for table names:
    const string developmentConnectionString = "UseDevelopmentStorage=true";
    const string developmentPrefix = "DivisiBillDebug";

    /// <summary>
    /// Decodes a base64-encoded connection string
    /// </summary>
    private static string DecodeConnectionString(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
            return "";

        try
        {
            byte[] data = Convert.FromBase64String(base64String);
            return System.Text.Encoding.UTF8.GetString(data);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Dictionary mapping Play Store keys and obfuscated account IDs (first 10 letters) to UserIds
    /// </summary>
    static readonly Dictionary<string, string> Mappings = new()
    {
        // Subscriptions
        { "GPA.3332-0658-5128-80451", "david.g.maw@gmail.com" },
        { "VM2UM7jbkJ", "david.g.maw@gmail.com" },

        { "GxdhAhyxRW", "support@autopl.us" },

        { "GPA.3357-5307-1564-25440", "Frank" },
        { "3bG7I1Y9BN", "Frank" },

        { "GPA.3312-8713-0461-22586", "Patti" },
        { "lhfFSTxxPC", "Patti" },

        { "GPA.3311-3772-9998-24344", "dgm@autopl.us" },
        { "Yltj9BQK3t", "dgm@autopl.us" },

        // OCR Licenses
        { "GPA.3319-4890-8422-52991", "support@autopl.us" },
        { "GPA.3334-3035-7547-40873", "david.g.maw@gmail.com" },

        // Obsolete keys
        { "GPA.3356-5864-8956-59211", "support@autopl.us old" },
        { "GPA.3319-1880-4012-27233", "support@autopl.us old" },
        { "GPA.3340-6117-3619-54222", "support@autopl.us old" },
        { "HsI9zUlKOc", "support@autopl.us old" },
        { "GPA.3365-9887-7867-99841", "support@autopl.us old" },
        { "zAtG1UAJeY", "support@autopl.us old" },
        { "GPA.3349-9523-9124-10936", "david.g.maw@gmail.com old" },
    };

    /// <summary>
    /// Collection of meal data
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<MealData> MealItems { get; set; } = [];

    /// <summary>
    /// Collection of license data
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<LicenseData> LicenseItems { get; set; } = [];

    /// <summary>
    /// Selected environment option for data retrieval
    /// </summary>
    [ObservableProperty]
    public partial EnvironmentOption SelectedEnvironment { get; set; } = EnvironmentOption.Development;

    /// <summary>
    /// Stores the previous environment for reverting on validation failure
    /// </summary>
    private EnvironmentOption previousEnvironment = EnvironmentOption.Development;

    /// <summary>
    /// Indicates whether a configuration error alert should be displayed
    /// </summary>
    [ObservableProperty]
    public partial bool ShowConfigurationError { get; set; }

    /// <summary>
    /// Error message for configuration issues
    /// </summary>
    [ObservableProperty]
    public partial string? ConfigurationErrorMessage { get; set; }

    /// <summary>
    /// Handles changes to the SelectedEnvironment property
    /// </summary>
    partial void OnSelectedEnvironmentChanged(EnvironmentOption oldValue, EnvironmentOption newValue)
    {
        var connectionString = GetConnectionStringForEnvironment(newValue);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ConfigurationErrorMessage = $"The connection string for {newValue} is not configured. Please add it to your settings.";
            ShowConfigurationError = true;
            SelectedEnvironment = oldValue;
            return;
        }

        previousEnvironment = newValue;
        MealItems.Clear();
        LicenseItems.Clear();
    }

    /// <summary>
    /// Available environment options for the picker
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<string> EnvironmentOptions { get; set; } = new() { "Development", "Alternate", "Production" };

    /// <summary>
    /// Status message for the last operation
    /// </summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>
    /// Indicates whether the view is currently loading data
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Constructor initializes the observable collections
    /// </summary>

    public string GetConnectionStringForEnvironment(EnvironmentOption environment)
    {
        return environment switch
        {
            EnvironmentOption.Production => productionConnectionString,
            EnvironmentOption.Alternate => alternateConnectionString,
            EnvironmentOption.Development => developmentConnectionString,
            _ => developmentConnectionString
        };
    }

    private (string connectionString, string prefix) GetConnectionStringAndPrefix(EnvironmentOption environment)
    {
        return environment switch
        {
            EnvironmentOption.Production => (productionConnectionString, productionPrefix),
            EnvironmentOption.Alternate => (alternateConnectionString, alternatePrefix),
            EnvironmentOption.Development => (developmentConnectionString, developmentPrefix),
            _ => (developmentConnectionString, developmentPrefix)
        };
    }

    private static string ExtractAccountName(string connectionString)
    {
        if (connectionString == developmentConnectionString)
            return "Azurite (local emulator)";
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            const string accountNamePrefix = "AccountName=";
            if (part.StartsWith(accountNamePrefix))
                return part[accountNamePrefix.Length..];
        }
        return "Unknown";
    }

    private static string? LookupUserId(string key)
    {
        if (Mappings.TryGetValue(key, out var userId))
            return userId;
        // Also try the first 10 characters
        if (key.Length >= 10 && Mappings.TryGetValue(key[..10], out var userId10))
            return userId10;
        return null;
    }

    /// <summary>
    /// Loads meal data from Azure Tables
    /// </summary>
    [RelayCommand]
    private async Task LoadMealData()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading meal data...";
            MealItems.Clear();

            var (connectionString, prefix) = GetConnectionStringAndPrefix(SelectedEnvironment);
            string tableName = prefix + "Meal";

            var tableClient = new TableClient(connectionString, tableName);
            Pageable<TableEntity> entities = tableClient.Query<TableEntity>();

            var mealData = new Dictionary<string, (string? userId, string partitionKey, DateTime? timestamp, int count)>();

            int mealEntities = 0;

            foreach (var entity in entities)
            {
                mealEntities++;
                var partitionKey = entity.PartitionKey;
                var timestamp = entity.Timestamp?.DateTime;

                string? userId = LookupUserId(partitionKey);

                if (mealData.TryGetValue(partitionKey, out var existing))
                {
                    DateTime? newLatestTimestamp = existing.timestamp;
                    if (timestamp.HasValue && (!existing.timestamp.HasValue || timestamp > existing.timestamp))
                        newLatestTimestamp = timestamp;

                    mealData[partitionKey] = (userId, partitionKey, newLatestTimestamp, existing.count + 1);
                }
                else
                    mealData[partitionKey] = (userId, partitionKey, timestamp, 1);
            }

            var sortedData = mealData.OrderByDescending(x => x.Value.timestamp);
            foreach (var kvp in sortedData)
            {
                var (userId, partitionKey, timestamp, count) = kvp.Value;
                MealItems.Add(new MealData
                {
                    UserId = userId ?? "",
                    PartitionKey = partitionKey,
                    LatestTimestamp = timestamp,
                    Count = count
                });
            }

            StatusMessage = $"Loaded {MealItems.Count} distinct meal groups (processed {mealEntities} entities) from {ExtractAccountName(connectionString)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading meal data: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Loads license data from Azure Tables
    /// </summary>
    [RelayCommand]
    private async Task LoadLicenseData()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading license data...";
            LicenseItems.Clear();

            var (connectionString, prefix) = GetConnectionStringAndPrefix(SelectedEnvironment);
            string tableName = prefix + "Licenses";

            var tableClient = new TableClient(connectionString, tableName);
            Pageable<TableEntity> entities = tableClient.Query<TableEntity>();

            var licenseData = new Dictionary<string, (string? userId, string accountId, string productId, DateTime? latestTimeUsed, int count)>();

            int licenseEntities = 0;

            foreach (var entity in entities)
            {
                licenseEntities++;
                var accountId = entity.GetString("ObfuscatedAccountId");
                if (string.IsNullOrEmpty(accountId))
                    accountId = entity.RowKey;

                var productId = entity.GetString("ProductId") ?? "N/A";
                var timeUsed = entity.GetDateTimeOffset("TimeUsed")?.DateTime ?? entity.Timestamp?.DateTime;

                string? userId = LookupUserId(accountId);

                string key = $"{accountId}|{productId}";

                if (licenseData.TryGetValue(key, out var existing))
                {
                    DateTime? newLatestTimeUsed = existing.latestTimeUsed;
                    if (timeUsed.HasValue && (!existing.latestTimeUsed.HasValue || timeUsed > existing.latestTimeUsed))
                        newLatestTimeUsed = timeUsed;
                    licenseData[key] = (userId, accountId, productId, newLatestTimeUsed, existing.count + 1);
                }
                else
                    licenseData[key] = (userId, accountId, productId, timeUsed, 1);
            }

            var sortedData = licenseData.OrderByDescending(x => x.Value.latestTimeUsed);
            foreach (var kvp in sortedData)
            {
                var (userId, accountId, productId, latestTimeUsed, count) = kvp.Value;
                LicenseItems.Add(new LicenseData
                {
                    UserId = userId ?? "",
                    ObfuscatedAccountId = accountId,
                    ProductId = productId,
                    LatestTimeUsed = latestTimeUsed,
                    Count = count
                });
            }

            StatusMessage = $"Loaded {LicenseItems.Count} distinct license groups (processed {licenseEntities} entities) from {ExtractAccountName(connectionString)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading license data: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
