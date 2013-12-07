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
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    public sealed class QuestionsManager
    {
        private static AutoResetEvent addEvent = new AutoResetEvent(true);
        private static StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
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

                string jsonString = await FilesManager.LoadAsync(storageFolder, settingsFileName);

                if (!JsonObject.TryParse(jsonString, out rootObject))
                {
                    Debug.WriteLine("Invalid JSON object: {0}", jsonString);
                    CreateFromScratch();
                    return;
                }

                questionsCollection = rootObject.GetNamedObject("Questions");
            });
        }

        public static void Unload()
        {
            rootObject = null;
        }

        public static IAsyncAction SaveAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await FilesManager.SaveAsync(storageFolder, settingsFileName, rootObject.Stringify());
            });
        }

        private static void CreateFromScratch()
        {
            rootObject = new JsonObject();

            questionsCollection = new JsonObject();
            rootObject.Add("Questions", questionsCollection);
        }

        // This method can be call simultaneously. Make sure only one thread is touching it.
        public static bool AddQuestions(string website, SyndicationFeed feed, bool skipLatestPubDate)
        {
            bool questionsChanged = false;

            DateTimeOffset lastestPubDate = SettingsManager.GetLastestPubDate(website);

            // Wait until the event is set by another thread.
            addEvent.WaitOne();

            try
            {
                CheckSettingsAreLoaded();

                foreach (SyndicationItem item in feed.Items)
                {
                    if (skipLatestPubDate || DateTimeOffset.Compare(item.PublishedDate.DateTime, lastestPubDate) > 0)
                    {
                        Debug.WriteLine("{0} > {1}", item.PublishedDate.DateTime, lastestPubDate);

                        if (AddQuestion(website, item))
                        {
                            questionsChanged = true;

                            if (item.PublishedDate > lastestPubDate)
                            {
                                lastestPubDate = item.PublishedDate;
                                Debug.WriteLine("New {0} LastestPubDate: {1}", website, lastestPubDate);
                            }
                        }
                    }
                    else
                    {
                        if (UpdateQuestion(website, item))
                        {
                            questionsChanged = true;
                        }
                    }
                }


                SettingsManager.SetLastestPubDate(website, lastestPubDate);

                return questionsChanged;
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

            // TODO: Get the newest question, and store the PubDate, so older questions aren't added to the list again.

            await SaveAsync();
        }

        // NOTE: Adding a single question does not load or save settings. Good for performance.
        private static bool AddQuestion(string website, SyndicationItem item)
        {
            // If the latestPubDate validation was skipped, it could happend that que query returns
            // questions we already have.
            if (questionsCollection.ContainsKey(item.Id))
            {
                return UpdateQuestion(website, item);
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
            questionObject.Add("Categories", categoriesCollection);

            // TODO: Remove this try, it is only for debug purposes.
            try
            {
                questionsCollection.Add(item.Id, questionObject);
            }
            catch (Exception)
            {
                JsonObject originalItem = questionsCollection.GetNamedObject(item.Id);
                string originalTitle = originalItem.GetNamedString("Title");
                string originalPubDate = originalItem.GetNamedString("PubDate");
                Debug.WriteLine(
                    "Question: {0} \r\nCurrent:  {1} {2} - {3}\r\nOriginal: {4} {5} - {6}", 
                    item.Id,
                    item.Title.Text,
                    item.PublishedDate,
                    item.PublishedDate.ToUniversalTime(),
                    originalTitle,
                    DateTimeOffset.Parse(originalPubDate),
                    DateTimeOffset.Parse( originalPubDate).ToUniversalTime());
                throw;
            }

            Debug.WriteLine("New question: {0}", item.Id);
            return true;
        }

        public static void RemoveQuestion(string id)
        {
            if (questionsCollection.ContainsKey(id))
            {
                questionsCollection.Remove(id);
            }
        }

        private static bool UpdateQuestion(string website, SyndicationItem item)
        {
            if (!questionsCollection.ContainsKey(item.Id))
            {
                Debug.WriteLine("Question skipped: {0}", item.Id);
                return false;
            }

            JsonObject questionObject = questionsCollection.GetNamedObject(item.Id);

            string oldTitle = questionObject.GetNamedString("Title");
            string newTitle = item.Title.Text;
            if (oldTitle != newTitle)
            {
                questionObject.SetNamedValue("Title", JsonValue.CreateStringValue(newTitle));
                Debug.WriteLine("Question updated: {0}", item.Id);
                return true;
            }

            Debug.WriteLine("Question up to date: {0}", item.Id);
            return false;
        }

        public static void ClearQuestions()
        {
            // TODO: Each question removed should be added to the "read questions list".

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

        public static IAsyncAction MoveFileFromLocalToRoaming()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                // Look in both folders.
                bool localExists = await FilesManager.FileExistsAsync(ApplicationData.Current.LocalFolder, settingsFileName);
                bool roamingExists = await FilesManager.FileExistsAsync(ApplicationData.Current.RoamingFolder, settingsFileName);

                if (localExists && !roamingExists)
                {
                    // File only in local folder. Move it to roaming folder.
                    await FilesManager.MoveAsync(
                        ApplicationData.Current.LocalFolder,
                        ApplicationData.Current.RoamingFolder,
                        settingsFileName);

                    Debug.WriteLine("File moved: {0}", settingsFileName);
                    return;
                }

                if (localExists && roamingExists)
                {
                    // File in both folders. Remove local version. Roaming version must come from a device
                    // with a more recent version of the app.
                    await FilesManager.DeleteAsync(ApplicationData.Current.LocalFolder, settingsFileName);

                    Debug.WriteLine("Duplicated file removed: {0}", settingsFileName);
                    return;
                }

                // Anything else means we have the configuration desired, or it is a new app.
                Debug.WriteLine("Desired situation: {0}", settingsFileName);
            });
        }
    }
}
