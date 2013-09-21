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
    public sealed class ContentManager
    {
        private static JsonObject rootObject;
        private static JsonObject tagsCollection;
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

                string jsonString = await SettingsManager.LoadAsync();

                if (!JsonObject.TryParse(jsonString, out rootObject))
                {
                    Debug.WriteLine("Invalid JSON string: {0}", jsonString);
                    InitializeEmptyCollections();
                    return;
                }

                if (!rootObject.ContainsKey("Tags") || !rootObject.ContainsKey("Questions"))
                {
                    Debug.WriteLine("Tampered JSON string.");
                    InitializeEmptyCollections();
                    return;
                }

                tagsCollection = rootObject.GetNamedObject("Tags");
                questionsCollection = rootObject.GetNamedObject("Questions");
            });
        }

        public static IAsyncAction SaveAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await SettingsManager.SaveAsync(rootObject.Stringify());
            });
        }

        private static void InitializeEmptyCollections()
        {
            rootObject = new JsonObject();
            tagsCollection = new JsonObject();
            questionsCollection = new JsonObject();
            rootObject.Add("Tags", tagsCollection);
            rootObject.Add("Questions", questionsCollection);
        }

        public static async void AddTag(ListView listView, string tag)
        {
            await LoadAsync();

            if (tagsCollection.ContainsKey(tag))
            {
                // We already have this tag.
                Debug.WriteLine("Tag repeated: {0}", tag);
                return;
            }

            JsonValue nullValue = JsonValue.Parse("null");
            tagsCollection.Add(new KeyValuePair<string, IJsonValue>(tag, nullValue));
            listView.Items.Add(tag);

            await SaveAsync();
        }

        public static void DeleteTag(ListView listView, string tag)
        {
            tagsCollection.Remove(tag);
            listView.Items.Remove(tag);
            ClearQuestions();
        }

        public static async void DisplayTags(ListView listView)
        {
            await LoadAsync();

            foreach (string key in tagsCollection.Keys)
            {
                listView.Items.Add(key);
            }
        }

        public static string ConcatenateAllTags()
        {
            CheckSettingsAreLoaded();

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

        public static async void AddQuestions(SyndicationFeed feed, bool skipLastAllRead)
        {
            CheckSettingsAreLoaded();

            DateTimeOffset lastAllRead = LastAllRead;

            foreach (SyndicationItem item in feed.Items)
            {
                Debug.WriteLine("PublishedDate: {0}", item.PublishedDate);
                if (skipLastAllRead || DateTimeOffset.Compare(item.PublishedDate.DateTime, lastAllRead) > 0)
                {
                    AddQuestion(item);
                }
            }

            await SaveAsync();
        }

        // NOTE: Adding a single question does not load or save settings. Good for performance.
        private static void AddQuestion(SyndicationItem item)
        {
            if (questionsCollection.ContainsKey(item.Id))
            {
                Debug.WriteLine("Question already exists.");
                return;
            }

            JsonObject questionObject = new JsonObject();

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

        public static bool TryCreateUri(string query, out Uri uri)
        {
            const string uriString = "http://stackoverflow.com/feeds/tag/";
            return Uri.TryCreate(uriString + query, UriKind.Absolute, out uri);
        }

        public static IAsyncOperation<bool> IsEmptyAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await LoadAsync();

                return (tagsCollection.Count == 0) ? true : false;
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
