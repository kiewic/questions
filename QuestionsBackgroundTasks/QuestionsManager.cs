using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    public sealed class QuestionsManager
    {
        private static AutoResetEvent addEvent = new AutoResetEvent(true);
        private const string settingsFileName = "questions.json";
        private static JsonObject rootObject;
        private static JsonObject questionsCollection;

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

            questionsCollection = new JsonObject();
            rootObject.Add("Questions", questionsCollection);
        }

        // This method can be call simultaneously. Make sure only one thread is touching it.
        public static void AddQuestions(string website, SyndicationFeed feed, bool skipLastAllRead)
        {
            // Wait until the event is set by another thread.
            addEvent.WaitOne();

            try
            {
                CheckSettingsAreLoaded();

                DateTimeOffset lastAllRead = ContentManager.LastAllRead;

                foreach (SyndicationItem item in feed.Items)
                {
                    if (skipLastAllRead || DateTimeOffset.Compare(item.PublishedDate.DateTime, lastAllRead) > 0)
                    {
                        Debug.WriteLine("Adding: {0}", item.Id);
                        AddQuestion(website, item);
                    }
                }
            }
            finally
            {
                // Set the event, so other threads waiting on it can do their job.
                addEvent.Set();
            }
        }

        public static async void LimitTo150AndSave()
        {
            Debug.WriteLine("Questions count before limit: {0}", questionsCollection.Count);

            const int questionsLimit = 150;
            if (questionsCollection.Count > questionsLimit)
            {
                JsonObject questionsCollectionCopy = new JsonObject();
                int length = questionsCollection.Count;
                int i = 0;

                IList<BindableQuestion> list = GetSortedQuestions();

                foreach (BindableQuestion question in list)
                {
                    questionsCollectionCopy.Add(question.Id, question.ToJsonObject());
                    if (++i == questionsLimit)
                    {
                        break;
                    }
                }

                questionsCollection = questionsCollectionCopy;
                rootObject.SetNamedValue("Questions", questionsCollectionCopy);
            }

            Debug.WriteLine("Questions count after limit: {0}", questionsCollection.Count);

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
                categoriesCollection.Add(category.Term, nullValue);
            }
            Debug.WriteLine("Categories: {0}", categoriesCollection.Stringify());
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
            foreach (var keyValuePair in questionsCollection)
            {
                BindableQuestion tempQuestion = new BindableQuestion(
                    keyValuePair.Key,
                    keyValuePair.Value.GetObject());

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

        private static void CheckSettingsAreLoaded()
        {
            if (rootObject == null)
            {
                throw new Exception("Questions not loaded.");
            }
        }

        internal static async Task<string> MigrateLastAllRead()
        {
            string value = "";

            await QuestionsManager.LoadAsync();

            if (rootObject.ContainsKey("LastAllRead"))
            {
                value = rootObject.GetNamedString("LastAllRead");
                rootObject.Remove("LastAllRead");
                await QuestionsManager.SaveAsync();
            }

            return value;
        }
    }
}
