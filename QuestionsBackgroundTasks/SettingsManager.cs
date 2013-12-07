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

        public static string Version
        {
            get
            {
                return roamingValues["Version"].ToString();
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

            InitializeRoamingSettings();
        }

        public static void Unload()
        {
            roamingValues = null;
            localValues = null;

            roamingWebsites = null;
            localWebsites = null;
        }

        private static void InitializeRoamingSettings()
        {
            if (!roamingValues.ContainsKey("Version"))
            {
                roamingValues["Version"] = "3";
            }

            if (!roamingValues.ContainsKey("UserId"))
            {
                // Generate a random user id.
                roamingValues["UserId"] = Guid.NewGuid().ToString();
            }

            if (!roamingValues.ContainsKey(WebsitesKey))
            {
                JsonObject jsonObject = new JsonObject();
                roamingValues[WebsitesKey] = jsonObject.Stringify();
            }

            if (!localValues.ContainsKey(WebsitesKey))
            {
                JsonObject jsonObject = new JsonObject();
                localValues[WebsitesKey] = jsonObject.Stringify();
            }

            // Parse roaming websites.
            string jsonString = roamingValues[WebsitesKey].ToString();
            roamingWebsites = JsonObject.Parse(jsonString);

            // Parse local websites.
            jsonString = localValues[WebsitesKey].ToString();
            localWebsites = JsonObject.Parse(jsonString);

            SyncWebsites();
        }

        public static void Save()
        {
            roamingValues[WebsitesKey] = roamingWebsites.Stringify();
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

                    Save();
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

            // Remove only questions containing this website. Then save, do not wait until save is completed.
            QuestionsManager.RemoveQuestions(websiteUrl, null);
            var saveOperation = QuestionsManager.SaveAsync();

            Save();
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
            Load();

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

        public static async void Import(StorageFile file)
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

            // Any value may be missing from the settings file, make sure all
            // values are initialized and websites are parsed.
            InitializeRoamingSettings();
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
            foreach (string website in localWebsites.Keys)
            {
                if (!roamingWebsites.ContainsKey(website))
                {
                    localWebsites.Remove(website);
                }
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
