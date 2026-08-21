using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DivisiBillWsClient.Services;
using DivisiBillWsClient.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace DivisiBillWsClient;

/// <summary>
/// View model for the main page. Handles communication with the storage web service,
/// file upload/download/delete operations and management of remote items.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    #region Shared State
    /// <summary>
    /// Shared <see cref="HttpClient"/> instance used for all web service requests.
    /// </summary>
    public static HttpClient Client { get; private set; } = new();

    /// <summary>
    /// Collection of available Base URLs to choose from.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<string> BaseUrlChoices { get; set; } = [];

    /// <summary>
    /// Temporary text for manual Entry editing - updates BaseUrl only on completion, see <see cref="CommitBaseUrlText"/>.
    /// </summary>
    [ObservableProperty]
    public partial string? BaseUrlText { get; set; }

    /// <summary>
    /// Base URL for all the remote functions provided by the web service.
    /// </summary>
    [ObservableProperty]
    public partial string BaseUrl { get; set; }

    partial void OnBaseUrlChanged(string value)
    {
        // Sync the text entry when BaseUrl changes (e.g., from Picker selection)
        BaseUrlText = value;
        StatusResponse = $"Base URL set to: {value}";

        if (Client.BaseAddress is not null) // Meaning it has already been used
        {
            Client = new(); // Reset the Client to avoid using the old base address
        }
        if (!string.IsNullOrWhiteSpace(value))
        {
            Client.BaseAddress = new Uri(value);
            SetPurchaseHeaders();

            // Determine and set the appropriate API key based on the URL
            string? keyToUse = null;

            if (!string.IsNullOrWhiteSpace(Generated.BuildInfo.DivisiBillWsUri) &&
                value.Equals(Generated.BuildInfo.DivisiBillWsUri, StringComparison.OrdinalIgnoreCase))
            {
                // URL matches the built-in DivisiBillWsUri, use its key
                keyToUse = Generated.BuildInfo.DivisiBillWsKey;
            }
            else if (!string.IsNullOrWhiteSpace(releaseUri) &&
                     value.Equals(releaseUri, StringComparison.OrdinalIgnoreCase))
            {
                // URL matches the DIVISIBILL_WS_URI_RELEASE environment variable, use its key
                keyToUse = releaseKey;
            }
            else if (!string.IsNullOrWhiteSpace(alternateUri) &&
                     value.Equals(alternateUri, StringComparison.OrdinalIgnoreCase))
            {
                // URL matches the DIVISIBILL_ALTERNATE_WS_URI environment variable, use its key
                keyToUse = alternateKey;
            }

            // Set the API key header if a key was determined
            if (!string.IsNullOrWhiteSpace(keyToUse))
            {
                UpsertHttpClientHeader(CallWs.KeyHeaderName, keyToUse);
            }
        }
    }

    private string? alternateUri;
    private string? releaseUri;
    private string? releaseKey;
    private string? alternateKey;

    /// <summary>
    /// Called when the view model is loaded. Initializes headers, base URL, password state
    /// and refreshes file and urlString lists from the service.
    /// </summary>
    public async Task OnLoadedAsync()
    {
        if (Client.BaseAddress is null) // Meaning initialization has not been done yet
        {
            List<string> possibleUrls = [];
            // These will not be present on Android/iOS but can be set in the IDE for testing on Windows
            var env = Environment.GetEnvironmentVariable("DIVISIBILL_WS_URI");
            if (!string.IsNullOrWhiteSpace(env))
                possibleUrls.Add(env);
            env = Environment.GetEnvironmentVariable("DIVISIBILL_WS_URI_LOCAL");
            if (!string.IsNullOrWhiteSpace(env))
                possibleUrls.Add(env);
            releaseUri = Environment.GetEnvironmentVariable("DIVISIBILL_WS_URI_RELEASE");
            if (!string.IsNullOrWhiteSpace(releaseUri))
                possibleUrls.Add(releaseUri);
            releaseKey = Environment.GetEnvironmentVariable("DIVISIBILL_WS_KEY_RELEASE");
            alternateUri = Environment.GetEnvironmentVariable("DIVISIBILL_ALTERNATE_WS_URI");
            if (!string.IsNullOrWhiteSpace(alternateUri))
                possibleUrls.Add(alternateUri);
            alternateKey = Environment.GetEnvironmentVariable("DIVISIBILL_ALTERNATE_WS_KEY");
            // This is an alternate URI that can be set for testing purposes and because it's created at build time will be present on Android.
            if (!string.IsNullOrWhiteSpace(Generated.BuildInfo.DivisiBillWsUri))
                possibleUrls.Add(Generated.BuildInfo.DivisiBillWsUri);
            // This is a temporary ngrok URL that can be used for testing purposes. It will be present on Android/iOS if the ngrok tunnel is active.
            possibleUrls.Add(Generated.BuildInfo.DivisiBillWsUriNgrok);
            foreach (var urlString in possibleUrls.Distinct())
                BaseUrlChoices.Add(urlString);
            BaseUrl = BaseUrlChoices[0];

            // Set up encryption state based on the obfuscatedAccountId from the DivisiBillTestPro purchase JSON.
            HasPassword = CryptManager.HasStoredPassword;
            string jsonString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Generated.BuildInfo.DivisiBillTestProJsonB64));
            JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
            if (jsonDoc.RootElement.TryGetProperty("obfuscatedAccountId", out JsonElement obfuscatedAccountIdElement))
            {
                CryptManager.PasswordSalt = obfuscatedAccountIdElement.GetString() ?? "";
            }

            // Check that round tripping the DivisiBillTestPro purchase JSON through deserialization and serialization preserves the signature.
            if (Billing.VerifyDivisiBillPurchaseSignature(jsonString, Generated.BuildInfo.DivisiBillTestProSignatureB64))
            {
                var purchase = Models.AndroidPurchase.FromJson(jsonString);
                if (purchase is null)
                    StatusResponse = "DivisiBillTestPro purchase deserialization failed.";
                else
                {
                    var reserialized = purchase.ToJsonString(); // Just to verify that it can be serialized back to JSON without error
                    if (Billing.VerifyDivisiBillPurchaseSignature(reserialized, Generated.BuildInfo.DivisiBillTestProSignatureB64))
                        StatusResponse = "DivisiBillTestPro signature verified successfully after round trip.";
                    else
                        StatusResponse = "DivisiBillTestPro signature verification failed after round trip.";
                }
            }
        }
    }

    /// <summary>
    /// Commits the manually typed <see cref="BaseUrlText"/> to <see cref="BaseUrl"/> when Entry editing is complete.
    /// </summary>
    [RelayCommand]
    private void CommitBaseUrlText()
    {
        BaseUrl = BaseUrlText ?? "";
    }

    #endregion
    #region File-related properties & methods
    /// <summary>
    /// Currently selected or entered file name for upload/download/delete operations.
    /// </summary>
    [ObservableProperty]
    public partial string? FileName { get; set; }

    /// <summary>
    /// Status text for most recent file operations.
    /// </summary>
    [ObservableProperty]
    public partial string? FileStatus { get; set; }

    /// <summary>
    /// Collection of <see cref="BlobItemInformation"/> objects returned from the files web service.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<BlobItemInformation> Files { get; set; } = new();

    /// <summary>
    /// The currently selected file from the files list.
    /// Setting this updates <see cref="FileName"/>.
    /// </summary>
    [ObservableProperty]
    public partial BlobItemInformation? SelectedFile { get; set; }
    partial void OnSelectedFileChanged(BlobItemInformation? value) => FileName = value?.Name;

    /// <summary>
    /// Refreshes the list of files asynchronously.
    /// </summary>
    /// <remarks>This method is typically invoked via a command binding to update the file list in the user
    /// interface. The operation completes when the file list has been refreshed.</remarks>
    /// <returns>A task that represents the asynchronous refresh operation.</returns>
    [RelayCommand]
    private async Task FileRefresh() => await RefreshFileListAsync();

    /// <summary>
    /// Uploads a file picked by the user. If a password is stored the file will be encrypted
    /// before upload. The encrypted or unencrypted counterpart will be deleted by the server.
    /// </summary>
    [RelayCommand]
    private async Task FileUpload()
    {
        Stream? stream = default;
        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select file to upload" });
            if (pick == null) return;

            FileName = pick.FileName;
            stream = await pick.OpenReadAsync();
            bool encrypt = CryptManager.HasStoredPassword;
            if (encrypt)
            {
                var encrypted = new MemoryStream();
                RSA? rsa = await CryptManager.GetStoredRsaFromFingerprintAsync();
                if (rsa is not null)
                {
                    await CryptManager.EncryptAsync(stream, encrypted, rsa);
                    encrypted.Position = 0;
                    stream.Close();
                    stream.Dispose();
                    stream = encrypted;
                }
            }
            string blobName = pick.FileName + (encrypt ? ".enc" : string.Empty);
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(blobName));
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"",
                FileName = "\"" + blobName + "\""
            };

            using var form = new MultipartFormDataContent
            {
                { content, "file", blobName }
            };

            var resp = await Client.PostAsync("file", form);
            resp.EnsureSuccessStatusCode();

            FileStatus = $"Uploaded {blobName}.";
            await RefreshFileListAsync();
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Upload failed", ex.Message);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Calls the "status/1" endpoint of the web service and updates <see cref="ItemStatus"/> with the response.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task CallStatus()
    {
        try
        {
            var resp = await Client.GetAsync("status/1");
            resp.EnsureSuccessStatusCode();
            var content = await resp.Content.ReadAsStringAsync();
            StatusResponse = Pretty(content);
        }
        catch (Exception ex)
        {
            StatusResponse = "Error: " + ex.Message;
        }
    }


    /// <summary>
    /// Calls the "status/1" endpoint of the web service and updates <see cref="ItemStatus"/> with the response.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task CallVersion()
    {
        try
        {
            bool gotVersion = await GetVersionAsync();
            StatusResponse = gotVersion ? MostRecentVersionInfo : "Failed to retrieve version.";
        }
        catch (Exception ex)
        {
            StatusResponse = "Error: " + ex.Message;
        }
    }
    /// <summary>
    /// Clears the <see cref="StatusResponse"/> property, effectively resetting the status display.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [RelayCommand]
    private async Task ClearStatusResponse()
    {
        StatusResponse = string.Empty;
    }

    /// <summary>
    /// Downloads the file with the name in <see cref="FileName"/>. If the blob is encrypted
    /// it will be decrypted before saving to the user-selected folder.
    /// </summary>
    [RelayCommand]
    private async Task FileDownload()
    {
        string? blobNameValue = FileName?.Trim();
        if (string.IsNullOrWhiteSpace(blobNameValue))
        {
            await Utilities.AlertAsync("Missing Filename", "Enter a blob name to download.");
            return;
        }
        bool encrypted = blobNameValue.EndsWith(".enc", StringComparison.OrdinalIgnoreCase);
        string fileNameValue = (encrypted) ? blobNameValue![..^4] : blobNameValue!;// lop off the .enc at the end if necessary
        if (string.IsNullOrWhiteSpace(fileNameValue))
        {
            await Utilities.AlertAsync("Encrypted Name Error ", "The encrypted blob name is too short.");
            return;
        }
        try
        {
            var response = await Client.GetAsync($"file/{Uri.EscapeDataString(blobNameValue)}", HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Prompt user to select a folder
            var folder = await FolderPicker.Default.PickAsync();

            if (folder?.Folder is null)
            {
                await Utilities.AlertAsync("Download cancelled", "No folder selected.");
                return;
            }

            var targetPath = Path.Combine(folder.Folder.Path, fileNameValue);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = File.Create(targetPath))
            {
                if (encrypted)
                {
                    using var decrypted = new MemoryStream();
                    await CryptManager.DecryptAsync(responseStream, decrypted);
                    decrypted.Position = 0;
                    await decrypted.CopyToAsync(fileStream);
                }
                else
                    await responseStream.CopyToAsync(fileStream);
            }

            FileStatus = $"Saved to: {targetPath}";
            await Utilities.AlertAsync("Downloaded", targetPath);
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Download failed", ex.Message);
        }
    }

    /// <summary>
    /// Deletes the file specified in <see cref="FileName"/> from the web service.
    /// </summary>
    [RelayCommand]
    private async Task FileDelete()
    {
        var name = FileName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await Utilities.AlertAsync("Missing filename", "Enter a file name to delete.");
            return;
        }
        try
        {
            var resp = await Client.DeleteAsync($"file/{Uri.EscapeDataString(name)}");
            resp.EnsureSuccessStatusCode();
            FileStatus = $"Deleted {name}.";
            await RefreshFileListAsync();
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Delete failed", ex.Message);
        }
    }

    /// <summary>
    /// Deletes the file specified in <see cref="FileName"/> from the web service.
    /// </summary>
    [RelayCommand]
    private async Task FileDeleteAll()
    {
        try
        {
            var resp = await Client.DeleteAsync($"files");
            resp.EnsureSuccessStatusCode();
            FileStatus = $"Deleted all files.";
            await RefreshFileListAsync();
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Delete All failed", ex.Message);
        }
    }
    /// <summary>
    /// Refreshes the list of files from the web service and updates the <see cref="Files"/> collection.
    /// </summary>
    private async Task RefreshFileListAsync()
    {
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var files = await Client.GetFromJsonAsync<List<BlobItemInformation>>("files", opts) ?? [];
            Files = [with(files.OrderByDescending((bi) => bi.Name))];
            FileStatus = $"Found {files.Count} file(s).";
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Returns a content type string appropriate for the provided file name.
    /// </summary>
    /// <param name="fileName">The file name to determine the content type for.</param>
    /// <returns>The MIME content type.</returns>
    private static string GetContentType(string fileName) => fileName switch
    {
        var f when f.EndsWith(".enc", StringComparison.OrdinalIgnoreCase) => "application/octet-stream",
        var f when f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) => "application/pdf",
        var f when f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) => "text/plain",
        var f when f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) => "image/jpeg",
        var f when f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => "image/png",
        _ => "application/octet-stream"
    };

    #endregion
    #region Item-related properties & methods

    /// <summary>
    /// Collection of <see cref="RemoteItemInfo"/> (PersonList, VenueList or Meal) returned from the web service.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<RemoteItemInfo> Items { get; set; } = new();

    /// <summary>
    /// Item type name used for access to person lists on the file web service.
    /// </summary>
    public const string PersonListTypeName = "personlist";

    /// <summary>
    /// Item type name used for venue lists on the file web service.
    /// </summary>
    public const string VenueListTypeName = "venuelist";

    /// <summary>
    /// Item type name used for meals on the file web service.
    /// </summary>
    public const string MealTypeName = "meal";

    public List<string> ItemTypeNames { get; } = [MealTypeName, PersonListTypeName, VenueListTypeName];

    /// <summary>
    /// The urlString type name used for the current operations (e.g., "meal", "personlist", or "venuelist").
    /// </summary>
    [ObservableProperty]
    public partial string ItemTypeName { get; set; } = MealTypeName;

    /// <summary>
    /// The currently selected remote urlString.
    /// Changing selection triggers loading of the selected urlString's data.
    /// </summary>
    [ObservableProperty]
    public partial RemoteItemInfo? SelectedItem { get; set; }
    partial void OnSelectedItemChanged(RemoteItemInfo? value) => _ = LoadSelectedItemDataAsync(value);

    /// <summary>
    /// Description text for the selected urlString.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedItemDescription { get; set; }

    /// <summary>
    /// Data (as string) for the selected urlString.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedItemData { get; set; }

    /// <summary>
    /// Status text for most recent urlString operations.
    /// </summary>
    [ObservableProperty]
    public partial string? ItemStatus { get; set; }

    /// <summary>
    /// Refreshes the list of items from the web service and updates the <see cref="Items"/> collection.
    /// </summary>
    [RelayCommand]
    private async Task ListItems()
    {
        try
        {
            SelectedItem = null;
            SelectedItemDescription = null;
            SelectedItemData = null;
            var remoteItems = await GetItemInfoListAsync(ItemTypeName);
            if (remoteItems is null) return;
            Items = [with(remoteItems)];
            ItemStatus = Items.Count == 1 ? $"Found one item." : $"Found {Items.Count} items.";
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Indicates whether a password is currently stored and will be used to encrypt data.
    /// Decryption is automatic, if needed, when retrieving data from the web service.
    /// </summary>
    [ObservableProperty]
    public partial bool HasPassword { get; set; }

    /// <summary>
    /// Opens the change password popup and updates stored password state when changed.
    /// </summary>
    [RelayCommand]
    private async Task ChangePassword()
    {
        var result = await Shell.Current.ShowPopupAsync<bool>(new ChangePasswordPopup());
        if (result.Result)
        {
            HasPassword = CryptManager.HasStoredPassword;
            await Utilities.AlertAsync("Password", "Password changed successfully.");
        }
    }

    /// <summary>
    /// Uploads the selected urlString to the web service. Uses encryption if a password is stored.
    /// </summary>
    [RelayCommand]
    private async Task ItemUpload()
    {
        if (SelectedItem is not RemoteItemInfo item)
        {
            await Utilities.AlertAsync("No urlString selected", "Select an urlString to upload.");
            return;
        }
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            await Utilities.AlertAsync("No Name", "Item selected had no name.");
            return;
        }

        try
        {
            string? itemData = await CallWs.GetItemAsStringAsync(ItemTypeName, item.Name);
            if (string.IsNullOrWhiteSpace(itemData))
            {
                await Utilities.AlertAsync("No Data", "Item selected had no data.");
                return;
            }
            bool b = await CallWs.PutItemAsync(ItemTypeName, item.Name, itemData, item.Description);
            if (!b)
            {
                await Utilities.AlertAsync("Upload failed", "Returned error result.");
                return;
            }
            item.IsEncrypted = CryptManager.HasStoredPassword;
            ItemStatus = $"Uploaded {(item.IsEncrypted ? "encrypted" : "plaintext")} urlString {item.Name}.";
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Upload failed", ex.Message);
        }
    }

    /// <summary>
    /// Deletes the currently selected urlString from the web service and removes it from the collection.
    /// </summary>
    [RelayCommand]
    private async Task ItemDelete()
    {
        if (SelectedItem is not RemoteItemInfo item)
        {
            await Utilities.AlertAsync("No urlString selected", "Select an urlString to delete.");
            return;
        }
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            await Utilities.AlertAsync("No Name", "Item selected had no name.");
            return;
        }

        try
        {
            await CallWs.DeleteItemAsync(ItemTypeName, item.Name);
            Items.Remove(item);
            SelectedItem = null;
            SelectedItemDescription = null;
            SelectedItemData = null;
            ItemStatus = $"Deleted urlString {item.Name}.";
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Delete failed", ex.Message);
        }
    }

    /// <summary>
    /// Deletes all items associated with the current meal type asynchronously and clears the local urlString collection.
    /// </summary>
    /// <remarks>After successful deletion, the local urlString list is cleared and any selected urlString or related
    /// data is reset. If the deletion fails, an alert is displayed to the user with the error message.</remarks>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    [RelayCommand]
    private async Task ItemDeleteAll()
    {
        try
        {
            ItemStatus = await CallWs.DeleteAllItemsAsync(ItemTypeName);
            Items.Clear();
            SelectedItem = null;
            SelectedItemDescription = null;
            SelectedItemData = null;
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Delete failed", ex.Message);
        }
    }

    /// <summary>
    /// Downloads the selected urlString as a plaintext XML file to a user-selected folder.
    /// <see cref="CallWs.GetItemAsStreamAsync"/> automatically decrypts data if necessary.
    /// </summary>
    [RelayCommand]
    private async Task ItemDownload()
    {
        if (SelectedItem is not RemoteItemInfo item)
        {
            await Utilities.AlertAsync("No urlString selected", "Select an urlString to download.");
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                await Utilities.AlertAsync("No Name", "Item selected had no name.");
                return;
            }
            using var stream = await CallWs.GetItemAsStreamAsync(ItemTypeName, item.Name);
            if (stream is null)
            {
                await Utilities.AlertAsync("Download failed", "No data returned.");
                return;
            }

            // Prompt user to select a folder
            FolderPickerResult? folder = await FolderPicker.Default.PickAsync();

            if (folder?.Folder == null)
            {
                await Utilities.AlertAsync("Download cancelled", "No folder selected.");
                return;
            }

            var fileName = $"{item.Name}.xml";
            var targetPath = Path.Combine(folder.Folder.Path, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using var fs = File.Create(targetPath);
            await stream.CopyToAsync(fs);

            ItemStatus = $"Saved to: {targetPath}";
            await Utilities.AlertAsync("Downloaded", targetPath);
        }
        catch (Exception ex)
        {
            await Utilities.AlertAsync("Download failed", ex.Message);
        }
    }

    /// <summary>
    /// Loads the plaintext data and description for the currently selected remote urlString.
    /// <see cref="CallWs.GetItemAsStringAsync"/> automatically decrypts if necessary.
    /// </summary>
    /// <param name="item">The selected remote urlString or null to clear selection values.</param>
    private async Task LoadSelectedItemDataAsync(RemoteItemInfo? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name))
        {
            SelectedItemDescription = null;
            SelectedItemData = null;
            return;
        }
        string? itemDataValue = await CallWs.GetItemAsStringAsync(ItemTypeName, item.Name);
        SelectedItemDescription = Pretty(item.Description);
        SelectedItemData = itemDataValue?.Replace("\n", " ").Replace("\r", " ");
    }
    #endregion
    #region Version Related
    /// <summary>
    /// Most recent version information returned from the service.
    /// </summary>
    internal static string MostRecentVersionInfo { get; set; } = string.Empty;

    /// <summary>
    /// Retrieves version information from the web service.
    /// </summary>
    /// <returns>True if version information was successfully retrieved and stored; otherwise false.</returns>
    private static async Task<bool> GetVersionAsync()
    {
        bool WsVersionChecked = false;
        try
        {
            HttpResponseMessage WsVersionTask = await Client.GetAsync("version");

            if (WsVersionTask != null && WsVersionTask.IsSuccessStatusCode)
            {
                MostRecentVersionInfo = await WsVersionTask.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(MostRecentVersionInfo))
                {
                    Debug.WriteLine("GetVersion returned OK but no data, returning NotFound");
                }
                else
                    WsVersionChecked = true;
            }
            else if (WsVersionTask == null)
                Debug.WriteLine("GetVersion failed, no task returned");
            else
                Debug.WriteLine("GetVersion failed, status code = " + WsVersionTask.StatusCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("GetVersion failed, exception = " + ex);
        }
        return WsVersionChecked;
    }
    #endregion
    #region Status API
    /// <summary>
    /// Status response text from the most recent call to the "status/1" endpoint.
    /// </summary>
    [ObservableProperty]
    public partial string StatusResponse { get; set; }
    #endregion
    #region Utility Functions
    /// <summary>
    /// Pretty-prints a JSON string for easier reading. Parses the input JSON and serializes it with indentation.
    /// </summary>
    /// <param name="json">The JSON string to pretty-print.</param>
    /// <returns>A formatted JSON string with indentation.</returns>
    private static string Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;
        var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Stores a token header from a web service response into the HTTP Client's default headers.
    /// </summary>
    /// <param name="response">The HTTP response containing headers to inspect.</param>
    private static void StoreTokenHeader(HttpResponseMessage response)
    {
        const string TokenHeaderName = "divisibill-token";

        string? tokenValue = response.Headers.Contains(TokenHeaderName) ? response.Headers.GetValues(TokenHeaderName).FirstOrDefault() : null;
        if (!string.IsNullOrWhiteSpace(tokenValue))
            UpsertHttpClientHeader(TokenHeaderName, tokenValue);
    }

    /// <summary>
    /// Ensures required headers are present on the HTTP Client (adds a purchase header and signature header). We need this because we do not call 
    /// <see cref="CallWs.VerifyPurchase(InAppBilling.InAppBillingPurchase)"/> directly in this context and it's what would normally set these headers.
    /// This is a workaround to ensure the headers are present for testing purposes.
    /// </summary>
    private void SetPurchaseHeaders()
    {
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Generated.BuildInfo.DivisiBillTestProJsonB64));
        UpsertHttpClientHeader(CallWs.PurchaseHeaderName, json);
        UpsertHttpClientHeader(CallWs.SignatureHeaderName, Generated.BuildInfo.DivisiBillTestProSignatureB64);
    }

    /// <summary>
    /// Adds or updates a header in the shared HTTP Client's default request headers.
    /// </summary>
    /// <param name="headerName">The header name.</param>
    /// <param name="headerValue">The header value.</param>
    private static void UpsertHttpClientHeader(string headerName, string headerValue)
    {
        if (Client.DefaultRequestHeaders.Contains(headerName))
            Client.DefaultRequestHeaders.Remove(headerName);
        Client.DefaultRequestHeaders.Add(headerName, headerValue);
    }
    #endregion
    #region Item List
    /// <summary>
    /// Represents a data urlString used in the Web communication, including its name, content, length, encryption status,
    /// and optional metadata.
    /// </summary>
    /// <remarks>This class encapsulates the properties of a single data urlString transmitted or received.
    /// It provides information about the urlString's identity, content, and whether the data is encrypted. The optional summary
    /// and remote image flags allow for additional metadata to be associated with the urlString. This type is intended for
    /// internal use within web API data handling scenarios.</remarks>
    private class WsDataItem(string name, long dataLength, string data, bool isEncrypted, string? summary = null)
    {
        public string? Name { get; set; } = name;
        public string? Data { get; set; } = data;
        public long DataLength { get; set; } = dataLength;
        public string? Summary { get; set; } = summary;
        public bool IsEncrypted { get; set; } = isEncrypted;
        public bool HasRemoteImage { get; set; } = false;
    }

    /// <summary>
    /// Retrieves a paged list of remote urlString information from the web service and returns
    /// a list of <see cref="RemoteItemInfo"/> instances. Handles decryption of summaries/data
    /// when items are encrypted.
    /// </summary>
    /// <param name="itemTypeName">The urlString type name to query (e.g. meal, personlist or venuelist).</param>
    /// <returns>A list of <see cref="RemoteItemInfo"/> or null if an error occurs.</returns>
    internal static async Task<List<RemoteItemInfo>?> GetItemInfoListAsync(string itemTypeName)
    {
        const int MaxItems = 500;
        if (string.IsNullOrWhiteSpace(itemTypeName))
            return null;

        List<RemoteItemInfo> remoteItemInfoList = [];
        string latestName = "30000000000000";
        try
        {
            while (true)
            {
                var itemListJson = await CallWs.GetItemsStreamAsync(itemTypeName, MaxItems, latestName);
                if (itemListJson != null && itemListJson.Length > 0)
                {
                    List<WsDataItem>? items = JsonSerializer.Deserialize<List<WsDataItem>>(itemListJson);
                    if (items == null)
                        break;
                    foreach (var item in items)
                    {
                        string? description = item.Summary;

                        if (item.IsEncrypted && !string.IsNullOrEmpty(item.Summary))
                        {
                            try
                            {
                                byte[] descriptionBytes = Convert.FromBase64String(item.Summary);
                                description = await CryptManager.DecryptB64StringAsync(item.Summary);
                            }
                            catch
                            {
                                // If decryption fails, fall back to the original (likely base64) summary
                                description = item.Summary;
                            }
                        }

                        // Item data is not normally returned from an urlString list but it can be in theory
                        string? itemData = item.Data;

                        if (item.IsEncrypted && !string.IsNullOrEmpty(itemData))
                        {
                            try
                            {
                                itemData = await CryptManager.DecryptB64StringAsync(itemData);
                            }
                            catch
                            {
                                // If decryption fails, fall back to the original (likely base64) summary
                                itemData = item.Data;
                            }
                        }

                        remoteItemInfoList.Add(new RemoteItemInfo()
                        {
                            Name = item.Name,
                            Size = item.DataLength,
                            IsEncrypted = item.IsEncrypted,
                            Description = description,
                            HasRemoteImage = item.HasRemoteImage,
                        });
                    }
                    if (items.Count < MaxItems)
                        break;
                    else
                    {
                        var lastName = items.Last()?.Name;
                        if (string.IsNullOrWhiteSpace(lastName))
                            break;
                        latestName = lastName;
                    }
                }
                else
                    break;
            }
            return remoteItemInfoList;
        }
        catch (Exception)
        {
            return null;
        }
    }
    #endregion
}