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

        public ContentManager()
        {
            Debug.WriteLine("Constructor being called!");
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

                string jsonString = await SettingsManager.LoadSettingsAsync();

                if (!JsonObject.TryParse(jsonString, out rootObject))
                {
                    Debug.WriteLine("Invalid JSON string: {0}", jsonString);
                    InitializeEmptyObjects();
                    return;
                }

                if (!rootObject.ContainsKey("Tags") || !rootObject.ContainsKey("Questions"))
                {
                    Debug.WriteLine("Tampered JSON string.");
                    InitializeEmptyObjects();
                    return;
                }

                tagsCollection = rootObject.GetNamedObject("Tags");
                questionsCollection = rootObject.GetNamedObject("Questions");
            });
        }

        private static void InitializeEmptyObjects()
        {
            rootObject = new JsonObject();
            tagsCollection = new JsonObject();
            questionsCollection = new JsonObject();
            rootObject.Add("Tags", tagsCollection);
            rootObject.Add("Questions", questionsCollection);
        }

        private static async Task SaveAsync()
        {
            await SettingsManager.SaveSettingsAsync(rootObject.Stringify());
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

        public static async void LoadTags(ListView listView)
        {
            await LoadAsync();

            foreach (string key in tagsCollection.Keys)
            {
                listView.Items.Add(key);
            }
        }

        public static string ConcatenateAllTags()
        {
            if (questionsCollection == null)
            {
                throw new Exception("Settings not loaded.");
            }

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

        public static async void AddQuestions(SyndicationFeed feed)
        {
            await LoadAsync();

            foreach (SyndicationItem item in feed.Items)
            {
                ContentManager.AddQuestion(item);
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
            questionObject.Add("PubDate", JsonValue.CreateStringValue(item.PublishedDate.ToLocalTime().ToString()));

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

        public static void LoadQuestions(ListView listView, IList<BindableQuestion> list)
        {
            listView.ItemsSource = list;
        }

        public static IList<BindableQuestion> GetSortedQuestions()
        {
            if (questionsCollection == null)
            {
                throw new Exception("Settings not loaded.");
            }

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
                return DateTime.Compare(a.PubDate, b.PubDate) * -1;
            });

            return list;
        }

        internal static void ClearQuestions()
        {
            if (questionsCollection == null)
            {
                throw new Exception("Settings not loaded.");
            }

            // Replace the collection with an empty object.
            questionsCollection = new JsonObject();
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

                return (questionsCollection.Count == 0) ? true : false;
            });
        }

    }
}
