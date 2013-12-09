using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    public sealed class SettingsManager
    {
        private const string LatestPubDateKey = "LatestPubDate";
        private const string LatestQueryDateKey = "LatestQueryDate";
        private const string WebsitesKey = "Websites";
        private static JsonObject roamingWebsites;
        private static JsonObject localWebsites;
        private static IPropertySet roamingValues;
        private static IPropertySet localValues;

        public static DateTimeOffset LatestQueryDate
        {
            get
            {
                CheckSettingsAreLoaded();

                if (localValues.ContainsKey(LatestQueryDateKey))
                {
                    return DateTimeOffset.Parse(localValues[LatestQueryDateKey].ToString());
                }

                return DateTime.MinValue;
            }
            set
            {
                localValues[LatestQueryDateKey] = value.ToString();
            }
        }

        public static void Load()
        {
            if (roamingValues != null && localValues != null)
            {
                // Settings already loaded, there is nothing to load.
                return;
            }

            ApplicationDataContainer roamingSettings = ApplicationData.Current.RoamingSettings;
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            roamingValues = roamingSettings.Values;
            localValues = localSettings.Values;

            Debug.WriteLine("Application data version: {0}", ApplicationData.Current.Version);
            Debug.WriteLine("Roaming settings storage quota: {0} KB", ApplicationData.Current.RoamingStorageQuota);
            Debug.WriteLine("Roaming settings folder: {0}", ApplicationData.Current.RoamingFolder.Path);
            Debug.WriteLine("Local settings folder: {0}", ApplicationData.Current.LocalFolder.Path);

            InitializeOrValidateSettings();
        }

        public static void Unload()
        {
            roamingValues = null;
            localValues = null;

            roamingWebsites = null;
            localWebsites = null;
        }

        private static void InitializeOrValidateSettings()
        {
            if (!roamingValues.ContainsKey("UserId"))
            {
                // Generate a random user id.
                roamingValues["UserId"] = Guid.NewGuid().ToString();
            }

            // Roaming websites.

            roamingWebsites = null;
            if (roamingValues.ContainsKey(WebsitesKey))
            {
                string jsonString = roamingValues[WebsitesKey].ToString();
                if (!JsonObject.TryParse(jsonString, out roamingWebsites))
                {
                    roamingWebsites = null;
                }
            }

            if (roamingWebsites == null)
            {
                roamingWebsites = new JsonObject();
                roamingValues[WebsitesKey] = roamingWebsites.Stringify();
            }

            if (roamingWebsites == null)
            {
                // This should not be null.
                Debugger.Break();
            }

            // Local websites.

            localWebsites = null;
            if (localValues.ContainsKey(WebsitesKey))
            {
                string jsonString = localValues[WebsitesKey].ToString();
                if (!JsonObject.TryParse(jsonString, out localWebsites))
                {
                    localWebsites = null;
                }
            }

            if (localWebsites == null)
            {
                localWebsites = new JsonObject();
                localValues[WebsitesKey] = localWebsites.Stringify();
            }

            if (localWebsites == null)
            {
                // This should not be null.
                Debugger.Break();
            }

            SyncWebsites();
        }

        // What is in roaming settings?
        //
        // * List of websites
        // * Tags, Name, ApiSiteParameter, IconUrl and FaviconUrl per website.
        //
        public static void SaveRoaming()
        {
            roamingValues[WebsitesKey] = roamingWebsites.Stringify();
        }

        // What is in local settings?
        //
        // * List of websites
        // * LastestPubDate per website.
        //
        public static void SaveLocal()
        {
            localValues[WebsitesKey] = localWebsites.Stringify();
        }

        public static IAsyncOperation<BindableWebsite> AddWebsiteAndSave(BindableWebsiteOption websiteOption)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                const int websitesLimit = 10;

                CheckSettingsAreLoaded();

                string websiteSiteUrl = websiteOption.SiteUrl;

                JsonObject roamingWebsiteObject = null;
                if (roamingWebsites.ContainsKey(websiteSiteUrl))
                {
                    // We already have this website. Nothing to do.
                    roamingWebsiteObject = roamingWebsites.GetNamedObject(websiteSiteUrl);
                }
                else if (roamingWebsites.Count < websitesLimit)
                {
                    roamingWebsiteObject = new JsonObject();
                    roamingWebsiteObject.SetNamedValue("Tags", new JsonObject());
                    roamingWebsiteObject.SetNamedValue("Name", JsonValue.CreateStringValue(websiteOption.ToString()));
                    roamingWebsiteObject.SetNamedValue("ApiSiteParameter", JsonValue.CreateStringValue(websiteOption.ApiSiteParameter));
                    roamingWebsiteObject.SetNamedValue("IconUrl", JsonValue.CreateStringValue(websiteOption.IconUrl));
                    roamingWebsiteObject.SetNamedValue("FaviconUrl", JsonValue.CreateStringValue(websiteOption.FaviconUrl));
                    roamingWebsites.SetNamedValue(websiteSiteUrl, roamingWebsiteObject);

                    JsonObject localWebsiteObject = new JsonObject();
                    localWebsites.SetNamedValue(websiteSiteUrl, localWebsiteObject);

                    SaveRoaming();
                    SaveLocal();
                }
                else
                {
                    var dialog = new MessageDialog("Only 10 websites allowed.", "Oops.");
                    await dialog.ShowAsync();

                    // Make sure to return null.
                    return null;
                }

                return new BindableWebsite(websiteSiteUrl, roamingWebsiteObject);
            });
        }

        public static void DeleteWebsiteAndSave(BindableWebsite website)
        {
            CheckSettingsAreLoaded();

            string websiteUrl = website.ToString();

            roamingWebsites.Remove(websiteUrl);
            localWebsites.Remove(websiteUrl);

            // Remove only questions containing this website.
            QuestionsManager.RemoveQuestionsAndSave(websiteUrl, null);

            SaveRoaming();
            SaveLocal();
        }

        internal static IEnumerable<string> GetWebsiteKeys()
        {
            return roamingWebsites.Keys;
        }

        internal static string GetWebsiteFaviconUrl(string website)
        {
            if (roamingWebsites.ContainsKey(website))
            {
                JsonObject websiteObject = roamingWebsites.GetNamedObject(website);
                if (websiteObject.ContainsKey("FaviconUrl"))
                {
                    return websiteObject.GetNamedString("FaviconUrl");
                }
            }

            return "";
        }

        public static void LoadAndDisplayWebsites(ListView listView)
        {
            Load();

            listView.Items.Clear();
            foreach (var keyValuePair in roamingWebsites)
            {
                var website = new BindableWebsite(keyValuePair.Key, keyValuePair.Value.GetObject());
                listView.Items.Add(website);
            }
        }

        public static string ConcatenateAllTags(string website)
        {
            CheckSettingsAreLoaded();

            JsonObject websiteObject = roamingWebsites.GetNamedObject(website);
            JsonObject tagsCollection = websiteObject.GetNamedObject("Tags");

            StringBuilder builder = new StringBuilder();
            foreach (string tag in tagsCollection.Keys)
            {
                if (builder.Length != 0)
                {
                    builder.Append(" OR ");
                }
                builder.Append(WebUtility.UrlEncode(tag));
            }
            return builder.ToString();
        }

        public static DateTimeOffset GetLastestPubDate(string websiteUrl)
        {
            CheckSettingsAreLoaded();

            JsonObject websiteObject = localWebsites.GetNamedObject(websiteUrl);

            if (websiteObject.ContainsKey(LatestPubDateKey))
            {
                string lastestPubDateString = websiteObject.GetNamedString(LatestPubDateKey);
                return DateTimeOffset.Parse(lastestPubDateString);
            }

            return DateTimeOffset.MinValue;
        }

        public static void SetLastestPubDate(string website, DateTimeOffset lastestPubDate)
        {
            CheckSettingsAreLoaded();

            JsonObject websiteObject = localWebsites.GetNamedObject(website);

            string lastestPubDateString = lastestPubDate.ToString();
            websiteObject.SetNamedValue(LatestPubDateKey, JsonValue.CreateStringValue(lastestPubDateString));
        }

        public static bool TryCreateUri(string website, string query, out Uri uri)
        {
            // If the query is empty, return the main feed URI.
            string uriString = website + "/feeds";

            // If the query is not empty, ask for the feed of the specified tags.
            if (!String.IsNullOrEmpty(query))
            {
                uriString += "/tag/" + query;
            }

            return Uri.TryCreate(uriString, UriKind.Absolute, out uri);
        }

        public static bool IsEmpty()
        {
            CheckSettingsAreLoaded();

            return (roamingWebsites.Count == 0) ? true : false;
        }

        private static void CheckSettingsAreLoaded()
        {
            if (roamingValues == null || localValues == null)
            {
                throw new Exception("Settings are not loaded.");
            }
        }

        public static async void Export(StorageFile file)
        {
            CheckSettingsAreLoaded();

            JsonObject jsonObject = new JsonObject();
            jsonObject.Add("Roaming", Export(roamingValues));
            jsonObject.Add("Local", Export(localValues));

            string jsonString = jsonObject.Stringify();
            await FileIO.WriteTextAsync(file, jsonString);
        }

        public static async void ImportAndSave(StorageFile file)
        {
            CheckSettingsAreLoaded();

            string jsonString = await FileIO.ReadTextAsync(file);

            JsonObject jsonObject;
            if (!JsonObject.TryParse(jsonString, out jsonObject))
            {
                // Invalid JSON string.
                // TODO: Notify user there was an error importing settings.
                Debugger.Break();
                return;
            }

            if (jsonObject.ContainsKey("Roaming"))
            {
                IJsonValue jsonValue = jsonObject.GetNamedValue("Roaming");
                if (jsonValue.ValueType == JsonValueType.Object)
                {
                    Import(roamingValues, jsonValue.GetObject());
                }
            }

            if (jsonObject.ContainsKey("Local"))
            {
                IJsonValue jsonValue = jsonObject.GetNamedValue("Local");
                if (jsonValue.ValueType == JsonValueType.Object)
                {
                    Import(localValues, jsonValue.GetObject());
                }
            }

            // Any value may be missing from the settings file, make sure all
            // values are initialized and websites are parsed.
            InitializeOrValidateSettings();

            SaveLocal();
            SaveRoaming();
        }

        private static JsonObject Export(IPropertySet values)
        {
            JsonObject jsonObject = new JsonObject();

            foreach (KeyValuePair<string, object> pair in values)
            {
                IJsonValue jsonValue;
                if (pair.Value == null)
                {
                    jsonValue = JsonValue.Parse("null");
                }
                else if (pair.Value is string)
                {
                    jsonValue = JsonValue.CreateStringValue((string)pair.Value);
                }
                else if (pair.Value is int)
                {
                    jsonValue = JsonValue.CreateNumberValue((int)pair.Value);
                }
                else if (pair.Value is double)
                {
                    jsonValue = JsonValue.CreateNumberValue((double)pair.Value);
                }
                else if (pair.Value is bool)
                {
                    jsonValue = JsonValue.CreateBooleanValue((bool)pair.Value);
                }
                else
                {
                    throw new Exception("Not supported type.");
                }

                jsonObject.Add(pair.Key, jsonValue);
            }

            return jsonObject;
        }

        private static void Import(IPropertySet values, JsonObject jsonObject)
        {
            values.Clear();

            foreach (string key in jsonObject.Keys)
            {
                IJsonValue jsonValue = jsonObject[key];

                switch (jsonValue.ValueType)
                {
                    case JsonValueType.String:
                        values.Add(key, jsonObject[key].GetString());
                        break;
                    case JsonValueType.Number:
                        values.Add(key, jsonObject[key].GetNumber());
                        break;
                    case JsonValueType.Boolean:
                        values.Add(key, jsonObject[key].GetBoolean());
                        break;
                    case JsonValueType.Null:
                        values.Add(key, null);
                        break;
                    default:
                        throw new Exception("Not supported JsonValueType.");
                }
            }
        }

        public static void SyncWebsites()
        {
            CheckSettingsAreLoaded();

            if (roamingWebsites.Count == localWebsites.Count)
            {
                // There is nothing to do.
                return;
            }

            // Remove from local websites not in roaming.
            List<string> keysToRemove = new List<string>();
            foreach (string website in localWebsites.Keys)
            {
                if (!roamingWebsites.ContainsKey(website))
                {
                    keysToRemove.Add(website);
                }
            }
            foreach (string key in keysToRemove)
            {
                localWebsites.Remove(key);
            }

            // Add websites from roaming into local.
            foreach (string website in roamingWebsites.Keys)
            {
                if (!localWebsites.ContainsKey(website))
                {
                    localWebsites.Add(website, new JsonObject());
                }
            }
        }
    }
}
