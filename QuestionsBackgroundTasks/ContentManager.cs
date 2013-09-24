using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    // TODO: Websites and tags does not change that frequently. But questions do.
    // Consider splitting questions managment from the rest of the settings.
    public sealed class ContentManager
    {
        private const string settingsFileName = "settings.json";
        private static JsonObject rootObject;
        private static JsonObject websitesCollection;
        private static JsonObject questionsCollection;

        public static DateTimeOffset LastAllRead
        {
            get
            {
                CheckSettingsAreLoaded();

                if (rootObject.ContainsKey("LastAllRead"))
                {
                    return DateTimeOffset.Parse(rootObject.GetNamedString("LastAllRead"));
                }

                return DateTime.MinValue;
            }
            set
            {
                rootObject.SetNamedValue("LastAllRead", JsonValue.CreateStringValue(value.ToString()));
            }
        }

        public ContentManager()
        {
            Debug.WriteLine("ContentManager constructor called.");
        }

        public static IAsyncAction LoadAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                if (rootObject != null)
                {
                    // Settings already loaded, there is nothing to do.
                    return;
                }

                string jsonString = await FilesManager.LoadAsync(settingsFileName);

                if (!JsonObject.TryParse(jsonString, out rootObject))
                {
                    Debug.WriteLine("Invalid JSON object: {0}", jsonString);
                    InitializeJsonValues();
                    return;
                }

                // Convert settings from version 1 to version 2.
                if (!rootObject.ContainsKey("Version"))
                {
                    rootObject.SetNamedValue("Version", JsonValue.CreateStringValue("2"));

                    websitesCollection = new JsonObject();
                    rootObject.SetNamedValue("Websites", websitesCollection);

                    if (rootObject.ContainsKey("Tags"))
                    {
                        JsonObject websiteObject = new JsonObject();
                        websiteObject.SetNamedValue("Tags", rootObject.GetNamedObject("Tags"));
                        websitesCollection.SetNamedValue("http://stackoverflow.com", websiteObject);

                        // TODO: Remove Tags object.
                    }
                }

                websitesCollection = rootObject.GetNamedObject("Websites");
                questionsCollection = rootObject.GetNamedObject("Questions");
            });
        }

        public static IAsyncAction SaveAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await FilesManager.SaveAsync(settingsFileName, rootObject.Stringify());
            });
        }

        private static void InitializeJsonValues()
        {
            rootObject = new JsonObject();
            rootObject.Add("Version", JsonValue.CreateStringValue("2"));

            websitesCollection = new JsonObject();
            rootObject.Add("Websites", websitesCollection);

            questionsCollection = new JsonObject();
            rootObject.Add("Questions", questionsCollection);
        }

        public static IAsyncOperation<BindableWebsite> AddWebsiteAndSave(BindableWebsiteOption websiteOption)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                CheckSettingsAreLoaded();

                string websiteSiteUrl = websiteOption.SiteUrl;

                JsonObject websiteObject;
                if (websitesCollection.ContainsKey(websiteSiteUrl))
                {
                    // We already have this website. Nothing to do.
                    websiteObject = websitesCollection.GetNamedObject(websiteSiteUrl);
                }
                else
                {
                    websiteObject = new JsonObject();
                    websiteObject.SetNamedValue("Tags", new JsonObject());
                    websiteObject.SetNamedValue("Name", JsonValue.CreateStringValue(websiteOption.ToString()));
                    websiteObject.SetNamedValue("ApiSiteParameter", JsonValue.CreateStringValue(websiteOption.ApiSiteParameter));
                    websiteObject.SetNamedValue("SiteUrl", JsonValue.CreateStringValue(websiteOption.SiteUrl));
                    websiteObject.SetNamedValue("IconUrl", JsonValue.CreateStringValue(websiteOption.IconUrl));
                    websiteObject.SetNamedValue("FaviconUrl", JsonValue.CreateStringValue(websiteOption.FaviconUrl));
                    websitesCollection.SetNamedValue(websiteSiteUrl, websiteObject);
                }

                await SaveAsync();

                return new BindableWebsite(websiteObject);
            });
        }

        public static async void DeleteWebsiteAndSave(BindableWebsite website)
        {
            CheckSettingsAreLoaded();

            websitesCollection.Remove(website.ToString());

            // TODO: Remove only questions containing this website.
            ClearQuestions();

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
            foreach (IJsonValue jsonValue in websitesCollection.Values)
            {
                var website = new BindableWebsite(jsonValue.GetObject());
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

        public static async void AddQuestions(string website, SyndicationFeed feed, bool skipLastAllRead)
        {
            CheckSettingsAreLoaded();

            DateTimeOffset lastAllRead = LastAllRead;

            foreach (SyndicationItem item in feed.Items)
            {
                Debug.WriteLine("PublishedDate: {0}", item.PublishedDate);
                if (skipLastAllRead || DateTimeOffset.Compare(item.PublishedDate.DateTime, lastAllRead) > 0)
                {
                    AddQuestion(website, item);
                }
            }

            await SaveAsync();
        }

        // NOTE: Adding a single question does not load or save settings. Good for performance.
        private static void AddQuestion(string website, SyndicationItem item)
        {
            if (questionsCollection.ContainsKey(item.Id))
            {
                Debug.WriteLine("Question already exists.");
                return;
            }

            JsonObject questionObject = new JsonObject();

            questionObject.Add("Website", JsonValue.CreateStringValue(website));
            questionObject.Add("Title", JsonValue.CreateStringValue(item.Title.Text));

            // TODO: Do we need to use PublihedDate.ToLocalTime(), or can we just work with the standard time?
            questionObject.Add("PubDate", JsonValue.CreateStringValue(item.PublishedDate.ToString()));

            if (item.Links.Count > 0)
            {
                questionObject.Add("Link", JsonValue.CreateStringValue(item.Links[0].Uri.AbsoluteUri));
            }

            JsonValue nullValue = JsonValue.Parse("null");
            JsonObject categoriesCollection = new JsonObject();
            foreach (SyndicationCategory category in item.Categories)
            {
                Debug.WriteLine("Category: {0}", category.Term);
                categoriesCollection.Add(category.Term, nullValue);
            }
            questionObject.Add("Categories", categoriesCollection);

            questionsCollection.Add(item.Id, questionObject);
        }

        public static void ClearQuestions()
        {
            // Replace the collection with an empty object.
            questionsCollection.Clear();
        }

        public static void DisplayQuestions(ListView listView, IList<BindableQuestion> list)
        {
            listView.ItemsSource = list;
        }

        // TODO: I fthis is an expensive operation, maybe we should consider to cache the result.
        public static IList<BindableQuestion> GetSortedQuestions()
        {
            CheckSettingsAreLoaded();

            List<BindableQuestion> list = new List<BindableQuestion>();
            foreach (IJsonValue jsonValue in questionsCollection.Values)
            {
                JsonObject jsonObject = jsonValue.GetObject();

                BindableQuestion tempQuestion = new BindableQuestion(jsonObject);

                list.Add(tempQuestion);
            }

            // Sort the items!
            list.Sort((a, b) =>
            {
                // Multiply by -1 to sort in ascending order.
                return DateTimeOffset.Compare(a.PubDate, b.PubDate) * -1;
            });

            return list;
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
