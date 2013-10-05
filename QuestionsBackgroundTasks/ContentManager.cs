using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    public sealed class ContentManager
    {
        private const string settingsFileName = "settings.json";
        private static JsonObject rootObject;
        private static JsonObject websitesCollection;
        private static ApplicationDataContainer roamingSettings;

        public static IAsyncAction LoadAsync()
        {

            return AsyncInfo.Run(async (cancellationToken) =>
            {
                if (roamingSettings != null)
                {
                    // Settings already loaded, there is nothing to load.
                    return;
                }

                roamingSettings = ApplicationData.Current.RoamingSettings;
                Debug.WriteLine("Roaming storage quota: {0} KB", ApplicationData.Current.RoamingStorageQuota);
                Debug.WriteLine("Roaming folder: {0}", ApplicationData.Current.RoamingFolder.Path);

                string version = roamingSettings.Values["Version"] as string;

                if (version == null)
                {
                    // Convert settings from version 2 to version 3.
                    await MigrateFrom2To3();
                }

                websitesCollection = JsonObject.Parse(roamingSettings.Values["Websites"].ToString());
            });
        }

        private static IAsyncAction MigrateFrom1To2()
        {
            rootObject.SetNamedValue("Version", JsonValue.CreateStringValue("2"));

            websitesCollection = new JsonObject();
            rootObject.SetNamedValue("Websites", websitesCollection);

            if (rootObject.ContainsKey("Tags"))
            {
                JsonObject websiteObject = new JsonObject();

                // Make tags collection a child of Stack Overflow website.
                websiteObject.SetNamedValue("Tags", rootObject.GetNamedObject("Tags"));
                rootObject.Remove("Tags");

                websiteObject.SetNamedValue("Name", JsonValue.CreateStringValue("Stack Overflow"));
                websiteObject.SetNamedValue("ApiSiteParameter", JsonValue.CreateStringValue("stackoverflow"));
                websiteObject.SetNamedValue("IconUrl", JsonValue.CreateStringValue("http://cdn.sstatic.net/stackoverflow/img/apple-touch-icon.png"));
                websiteObject.SetNamedValue("FaviconUrl", JsonValue.CreateStringValue("http://cdn.sstatic.net/stackoverflow/img/favicon.ico"));

                websitesCollection.SetNamedValue("http://stackoverflow.com", websiteObject);
            }

            if (rootObject.ContainsKey("Questions"))
            {
                // Remove questions, questions are now on a different file.
                rootObject.Remove("Questions");
            }

            return SaveAsync();
        }

        private static async Task MigrateFrom2To3()
        {
            string jsonString = await FilesManager.LoadAsync(settingsFileName);

            if (!JsonObject.TryParse(jsonString, out rootObject))
            {
                Debug.WriteLine("Invalid JSON object: {0}", jsonString);
                InitializeJsonValues();
                return;
            }

            if (!rootObject.ContainsKey("Version"))
            {
                // Convert settings from version 1 to version 2.
                await MigrateFrom1To2();
            }

            // Version.
            roamingSettings.Values["Version"] = "3";

            // Websites.
            websitesCollection = rootObject.GetNamedObject("Websites");
            roamingSettings.Values["Websites"] = websitesCollection.Stringify();

            // LastAllRead should be also synched across devices.
            string lastAllRead = await QuestionsManager.MigrateLastAllRead();
            if (!String.IsNullOrEmpty(lastAllRead))
            {
                roamingSettings.Values["LastAllRead"] = lastAllRead;
            }

            await FilesManager.DeleteAsync(settingsFileName);
        }

        // TODO: Make this method synchronous.
        public static IAsyncAction SaveAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                roamingSettings.Values["Websites"] = websitesCollection.Stringify();
            });
        }

        private static void InitializeJsonValues()
        {
            rootObject = new JsonObject();
            rootObject.Add("Version", JsonValue.CreateStringValue("2"));

            websitesCollection = new JsonObject();
            rootObject.Add("Websites", websitesCollection);
        }

        public static IAsyncOperation<BindableWebsite> AddWebsiteAndSave(BindableWebsiteOption websiteOption)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                const int websitesLimit = 10;

                CheckSettingsAreLoaded();

                string websiteSiteUrl = websiteOption.SiteUrl;

                JsonObject websiteObject = null;
                if (websitesCollection.ContainsKey(websiteSiteUrl))
                {
                    // We already have this website. Nothing to do.
                    websiteObject = websitesCollection.GetNamedObject(websiteSiteUrl);
                }
                else if (websitesCollection.Count < websitesLimit)
                {
                    websiteObject = new JsonObject();
                    websiteObject.SetNamedValue("Tags", new JsonObject());
                    websiteObject.SetNamedValue("Name", JsonValue.CreateStringValue(websiteOption.ToString()));
                    websiteObject.SetNamedValue("ApiSiteParameter", JsonValue.CreateStringValue(websiteOption.ApiSiteParameter));
                    websiteObject.SetNamedValue("IconUrl", JsonValue.CreateStringValue(websiteOption.IconUrl));
                    websiteObject.SetNamedValue("FaviconUrl", JsonValue.CreateStringValue(websiteOption.FaviconUrl));
                    websitesCollection.SetNamedValue(websiteSiteUrl, websiteObject);

                    await SaveAsync();
                }
                else
                {
                    var dialog = new MessageDialog("Only 10 websites allowed.", "Oops.");
                    await dialog.ShowAsync();

                    // Make sure to return null.
                    return null;
                }

                return new BindableWebsite(websiteSiteUrl, websiteObject);
            });
        }

        public static async void DeleteWebsiteAndSave(BindableWebsite website)
        {
            CheckSettingsAreLoaded();

            websitesCollection.Remove(website.ToString());

            // TODO: Remove only questions containing this website.
            QuestionsManager.ClearQuestions();

            await SaveAsync();
        }

        internal static IEnumerable<string> GetWebsiteKeys()
        {
            return websitesCollection.Keys;
        }

        public static async void LoadAndDisplayWebsites(ListView listView)
        {
            await LoadAsync();

            listView.Items.Clear();
            foreach (var keyValuePair in websitesCollection)
            {
                var website = new BindableWebsite(keyValuePair.Key, keyValuePair.Value.GetObject());
                listView.Items.Add(website);
            }
        }

        public static string ConcatenateAllTags(string website)
        {
            CheckSettingsAreLoaded();

            JsonObject websiteObject = websitesCollection.GetNamedObject(website);
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

        public static IAsyncOperation<bool> IsEmptyAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await LoadAsync();

                return (websitesCollection.Count == 0) ? true : false;
            });
        }

        private static void CheckSettingsAreLoaded()
        {
            if (rootObject == null)
            {
                throw new Exception("Settings not loaded.");
            }
        }
    }
}
