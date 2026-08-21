using DivisiBillWsClient.InAppBilling;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace DivisiBillWsClient.Services
{
    internal class CallWs
    {
        internal const string PurchaseHeaderName = "divisibill-android-purchase";
        internal const string TokenHeaderName = "divisibill-token";
        internal const string SignatureHeaderName = "divisibill-signature";
        internal const string KeyHeaderName = "x-functions-key";
        private static readonly HttpClient client = MainPageViewModel.Client;
        #region Header Management
        private static void StoreTokenHeader(HttpResponseMessage response)
        {
            string? tokenValue = response.Headers.Contains(TokenHeaderName) ? response.Headers.GetValues(TokenHeaderName).FirstOrDefault() : null;
            if (!string.IsNullOrWhiteSpace(tokenValue))
                UpsertHttpClientHeader(TokenHeaderName, tokenValue);
        }
        public static void UpsertHttpClientHeader(string headerName, string headerValue)
        {
            if (client.DefaultRequestHeaders.Contains(headerName))
                client.DefaultRequestHeaders.Remove(headerName);
            client.DefaultRequestHeaders.Add(headerName, headerValue);
        }
        #endregion

        /// <summary>
        /// Invoke a web service, wait a few seconds to see if it completes, then pop up a dialog so the user can check progress and abandon it as needed.
        /// </summary>
        /// <param name="webCall">The function to call and timeout if necessary</param>
        /// 
        /// <returns></returns>
        public static async Task<HttpResponseMessage> CallUncertainWebServiceAsync(Func<CancellationTokenSource, Task<HttpResponseMessage>> webCall)
        {
            return await webCall(new CancellationTokenSource());
        }
        #region Purchase and Verify
        /// <summary>
        /// Make a record of a new purchase
        /// </summary>
        /// <param name="purchase"></param>
        /// <returns>True if the purchase was recorded, false if not</returns>
        internal static async Task<bool> RecordPurchaseAsync(InAppBillingPurchase purchase)
        {
            if (DeviceInfo.Platform == DevicePlatform.Android && purchase.OriginalJson is not null && purchase.Signature is not null)
            {
                // Store the license by calling a web service
                try
                {
                    Dictionary<string, string> formData = new()
                {
                    { "purchase", purchase.OriginalJson },
                    { "signature", purchase.Signature }
                };
                    FormUrlEncodedContent content = new(formData);
                    HttpResponseMessage response = await client.PostAsync("RecordAndroidPurchase?subscription=", content);
                    return response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    Utilities.DebugMsg("RecordPurchaseAsync failed, exception = " + ex);
                }
            }
            return false;
        }

        /// <summary>
        /// Verify that an InAppBilling purchase really is what it pretends to be and that we previously purchased it.
        /// Currently only implemented for Android.
        /// </summary>
        /// <remarks>
        /// This method is always called at least once per initialization of the app to verify that the pro license is still valid
        /// and record its value for future web service calls. It is also called when a new purchase is made to verify that the
        /// purchase is valid and record it for future use. If verification fails, it will return null and the license will be
        /// considered invalid.
        /// </remarks>
        /// <param name="purchase">The InAppBilling object to be tested</param>
        /// <returns>The contents of the returned verification message or null if verification failed</returns>
        internal static async Task<string?> VerifyPurchase(InAppBillingPurchase purchase)
        {
            Utilities.DebugMsg("In VerifyPurchase for " + purchase.Id);
            if ((DeviceInfo.Platform == DevicePlatform.Android || (DeviceInfo.Platform == DevicePlatform.WinUI && Utilities.IsDebug)) && purchase.OriginalJson is not null && purchase.Signature is not null)
            {
                try
                {
                    Dictionary<string, string> formData = new()
                {
                    { "purchase", purchase.OriginalJson },
                    { "signature", purchase.Signature }
                };
                    FormUrlEncodedContent content = new(formData);
                    Utilities.DebugMsg("In VerifyPurchase, awaiting VerifyAndroidPurchase");
                    // validate the license by calling a web service
                    HttpResponseMessage response = await CallUncertainWebServiceAsync((CancellationTokenSource cts) => client.PostAsync("VerifyAndroidPurchase", content, cts.Token));
                    if (response.IsSuccessStatusCode && purchase.ProductId is not null)
                    {
                        string s = await response.Content.ReadAsStringAsync();
                        Utilities.RecordMsg("In VerifyPurchase, VerifyAndroidPurchase returned ok and \"" + s + "\"");
                        // If this is a pro license, pass it to future web service calls for authorization
                        if (purchase.ProductId.Equals(Billing.ProSubscriptionId) || purchase.ProductId.Equals(Billing.OldProProductId))
                        {
                            UpsertHttpClientHeader(PurchaseHeaderName, purchase.OriginalJson); // This will be the license used from now on
                            UpsertHttpClientHeader(SignatureHeaderName, purchase.Signature);
                            response.StoreTokenHeader();
                        }
                        return s;
                    }
                    else if (response.StatusCode == HttpStatusCode.RequestTimeout)
                    {
                        Utilities.RecordMsg("In VerifyPurchase, verify returned timeout, so remote services are unavailable");
                        return "-408"; // for 408-RequestTimeout, we return a string instead of null to indicate that the failure was due to remote services being unavailable instead of the purchase not being valid 
                    }
                    else
                        Utilities.RecordMsg("In VerifyPurchase, verify returned status " + (int)response.StatusCode + "-" + response.StatusCode + " and '" + await response.Content.ReadAsStringAsync() + "'");
                }
                catch (Exception ex)
                {
                    Utilities.RecordMsg("Exception in VerifyPurchase for " + purchase.Id + ": " + ex.Message);
                }
            }
            else
                Utilities.DebugMsg("In VerifyPurchase, not Android");
            Utilities.DebugMsg("Leaving VerifyPurchase, returning null");
            return null;
        }

        /// <summary>
        /// Verify that an InAppBilling purchase really is what it pretends to be and also that we own it.
        /// Fails silently if verification cannot be performed. Currently only implemented for Android.
        /// </summary>
        /// <remarks>
        /// This method will not throw exceptions if verification fails. It is designed to be used in scenarios
        /// where a failed verification should not disrupt the user experience, such as doing a periodic reverification of a
        /// pro subscription. If you need to know the reason for a failed verification or have the current pro license recorded, 
        /// use <see cref="VerifyPurchase(InAppBillingPurchase)"/> instead.
        /// </remarks>
        /// <param name="purchase">The InAppBilling object to be tested</param>
        /// <returns>True if the purchase is verified, false otherwise</returns>
        internal static async Task<bool> TryVerifyPurchase(InAppBillingPurchase purchase)
        {
            Utilities.DebugMsg($"In TryVerifyPurchase for {purchase.Id}");
            if ((DeviceInfo.Platform == DevicePlatform.Android || (DeviceInfo.Platform == DevicePlatform.WinUI && Utilities.IsDebug)) && purchase.OriginalJson is not null && purchase.Signature is not null)
            {
                try
                {
                    Dictionary<string, string> formData = new()
                {
                    { "purchase", purchase.OriginalJson },
                    { "signature", purchase.Signature }
                };
                    FormUrlEncodedContent content = new(formData);
                    Utilities.DebugMsg("In TryVerifyPurchase, awaiting VerifyAndroidPurchase");
                    // validate the license by calling a web service
                    HttpResponseMessage response = await client.PostAsync("VerifyAndroidPurchase", content);
                    if (response.IsSuccessStatusCode && purchase.ProductId is not null)
                    {
                        Utilities.DebugMsg("In TryVerifyPurchase, verify returned status ok");
                        return true;
                    }
                    else
                        Utilities.DebugMsg("In TryVerifyPurchase, verify returned status " + (int)response.StatusCode + "-" + response.StatusCode + " and '" + await response.Content.ReadAsStringAsync() + "'");
                }
                catch (Exception ex)
                {
                    Utilities.RecordMsg("Exception in TryVerifyPurchase for " + purchase.Id + ": " + ex.Message);
                }
            }
            else
                Utilities.DebugMsg("In TryVerifyPurchase, not Android");
            Utilities.DebugMsg("Leaving TryVerifyPurchase, returning false");
            return false;
        }
        #endregion
        #region CRUD operations on Meal/VenueList/PersonList
        /// <summary>
        /// Get a single item (Meal, PersonList or VenueList)
        /// </summary>
        /// <param name="itemTypeName">The item type ("meal"/VenueListTypeName/"personList")</param>
        /// <param name="id">Name of the item to be retrieved</param>
        /// <returns>The item data (even for meal items), normally an XML encoded object</returns>
        /// 
        public static async Task<string?> GetItemAsStringAsync(string itemTypeName, string id)
        {
            HttpResponseMessage response = await client.GetAsync($"{itemTypeName}/{id}");
            if (response.IsSuccessStatusCode)
            {
                StoreTokenHeader(response);
                bool isEncrypted = string.Equals(response.Content.Headers.ContentType?.MediaType, "application/octet-stream");
                if (isEncrypted)
                {
                    byte[] encryptedBytes = await response.Content.ReadAsByteArrayAsync();
                    byte[] plaintextBytes = await Task.Run(() => CryptManager.DecryptToBytes(encryptedBytes));
                    return Encoding.UTF8.GetString(plaintextBytes);
                }
                else
                    return await response.Content.ReadAsStringAsync();
            }
            else
                return null;
        }
        public static async Task<Stream?> GetItemAsStreamAsync(string itemTypeName, string id)
        {
            HttpResponseMessage response = await client.GetAsync($"{itemTypeName}/{id}");
            if (response.IsSuccessStatusCode)
            {
                StoreTokenHeader(response);
                bool isEncrypted = string.Equals(response.Content.Headers.ContentType?.MediaType, "application/octet-stream");
                if (isEncrypted)
                {
                    byte[] encryptedBytes = await response.Content.ReadAsByteArrayAsync();
                    byte[] plaintextBytes = await Task.Run(() => CryptManager.DecryptToBytes(encryptedBytes));
                    return new MemoryStream(plaintextBytes);
                }
                else
                {
                    return await response.Content.ReadAsStreamAsync();
                }
            }
            else
                return null;
        }

        /// <summary>
        /// Store a single item by sending multiple form fields
        /// </summary>
        /// <param name="itemTypeName">The item type ("meal"/VenueListTypeName/"personlist")</param>
        /// <param name="id">Name of the item</param>
        /// <param name="itemData">Data associated with the item</param>
        /// <param name="itemSummary">Summary data for the item (valid only for meal items</param>
        /// <returns>true of the put worked, false if not</returns>
        public static async Task<bool> PutItemAsync(string itemTypeName, string id, string itemData, string? itemSummary = null)
        {
            RSA? rsa = null;
            // Local function to create form content for a field
            async Task<HttpContent> FormContent(string fieldName, string fieldValue)
            {
                HttpContent itemDataContent;
                // Build data content (encrypt if RSA available)
                if (rsa is not null)
                {
                    byte[] plaintext = Encoding.UTF8.GetBytes(fieldValue);
                    byte[] encrypted = await Task<byte[]>.Run(() => CryptManager.EncryptToBytes(plaintext, rsa));
                    itemDataContent = new ByteArrayContent(encrypted);
                    itemDataContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    itemDataContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = fieldName,
                        FileName = fieldName
                    };
                }
                else
                {
                    itemDataContent = new StringContent(fieldValue, Encoding.UTF8, "application/xml");
                    itemDataContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = fieldName };
                }
                return itemDataContent;
            }

            // Optionally load RSA for encryption
            if (CryptManager.HasStoredPassword)
            {
                rsa = await CryptManager.GetStoredRsaFromFingerprintAsync();
            }

            var multipartFormDataContent = new MultipartFormDataContent
            {
                await FormContent("data", itemData)
            };
            if (itemSummary != null)
                multipartFormDataContent.Add(await FormContent("summary", itemSummary));

            // Call the web service and show the response 
            string? responseData = null;
            try
            {
                HttpResponseMessage response = await client.PutAsync($"{itemTypeName}/{id}", multipartFormDataContent);
                StoreTokenHeader(response);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(responseData))
                    throw;
                else
                    throw new HttpRequestException(ex.Message + "\n\n" + System.Text.RegularExpressions.Regex.Unescape(responseData), ex);
            }
            finally
            {
                multipartFormDataContent.Dispose();
                rsa?.Dispose();
            }
        }
        public static async Task<string> DeleteItemAsync(string itemTypeName, string id)
        {
            HttpResponseMessage response = await client.DeleteAsync($"{itemTypeName}/{id}");
            StoreTokenHeader(response);
            string temp = await response.Content.ReadAsStringAsync();
            return temp;
        }
        public static async Task<string> GetItemsStringAsync(string itemTypeName, int top = 50, string before = "30000000000000")
        {
            var content = await GetItemsAsync(itemTypeName, top, before);
            return await content.ReadAsStringAsync();
        }
        public static async Task<Stream> GetItemsStreamAsync(string itemTypeName, int top = 50, string before = "30000000000000")
        {
            var content = await GetItemsAsync(itemTypeName, top, before);
            return await content.ReadAsStreamAsync();
        }
        private static async Task<HttpContent> GetItemsAsync(string itemTypeName, int top, string before)
        {
            string param = "?top=" + top.ToString();
            if (!string.IsNullOrWhiteSpace(before))
                param += "&before=" + before;
            HttpResponseMessage response = await client.GetAsync(itemTypeName + "s" + param);
            StoreTokenHeader(response);
            var temp = response.Content;
            return temp;
        }
        public static async Task<string> DeleteAllItemsAsync(string itemTypeName)
        {
            HttpResponseMessage response = await client.DeleteAsync(itemTypeName + "s");
            StoreTokenHeader(response);
            string temp = await response.Content.ReadAsStringAsync();
            return temp;
        }
        #endregion
    }

    public static class HttpResponseMessageExtensions
    {
        public static void StoreTokenHeader(this HttpResponseMessage response)
        {
            string? tokenValue = response.Headers.Contains("divisibill-token") ? response.Headers.GetValues("divisibill-token").FirstOrDefault() : null;
            if (!string.IsNullOrWhiteSpace(tokenValue))
                CallWs.UpsertHttpClientHeader("divisibill-token", tokenValue);
        }
    }
}
