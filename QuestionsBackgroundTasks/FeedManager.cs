using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.Web.Syndication;

namespace QuestionsBackgroundTasks
{
    public sealed class FeedManager
    {
        private static SyndicationClient client = new SyndicationClient();

        // 1. Load settings.
        // 2. query -> Retrieve feeds.
        // 3. skipLatestPubDate -> Add questions.
        // 4. Sort questions (needed to show tile and badge).
        // 5. Update tile.
        // 6. Update badge.
        // TODO: Maybe check for buzz words here.
        public static IAsyncAction QueryWebsitesAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                SettingsManager.Load();
                await QuestionsManager.LoadAsync();

                // Query websites in parallel.
                List<Task<QuerySingleWebsiteResult>> tasks = new List<Task<QuerySingleWebsiteResult>>();
                IEnumerable<string> keys = SettingsManager.GetWebsiteKeys();
                foreach (string website in keys)
                {
                    string query = SettingsManager.ConcatenateAllTags(website);
                    tasks.Add(QuerySingleWebsiteAsync(website, query, false).AsTask());
                }

                if (tasks.Count == 0)
                {
                    // There is nothing to wait.
                    return;
                }

                await Task.Factory.ContinueWhenAll(tasks.ToArray(), (tasks2) => {});

                bool listChanged = false;
                foreach (Task<QuerySingleWebsiteResult> task in tasks)
                {
                    QuerySingleWebsiteResult result = task.Result;
                    if (result.Changed)
                    {
                        listChanged = true;
                    }
                }

                // Only limit and save questions if list changed.
                if (listChanged)
                {
                    Debug.WriteLine("Questions list changed.");
                    QuestionsManager.LimitTo150AndSave();
                    SettingsManager.Save(); // Save updated latestPubDate.
                    UpdateTileAndBadge();
                }
                else
                {
                    Debug.WriteLine("Questions list did not change.");
                }

                // Update last query date/time.
                // TODO: Only modify date/time if all queries were successful.
                SettingsManager.LatestQueryDate = DateTimeOffset.Now;
            });
        }

        public static IAsyncOperation<QuerySingleWebsiteResult> QuerySingleWebsiteAsync(
            string website,
            string query,
            bool skipLatestPubDate)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                QuerySingleWebsiteResult result = new QuerySingleWebsiteResult();

                Uri uri;
                if (!SettingsManager.TryCreateUri(website, query, out uri))
                {
                    Debugger.Break();
                }

                SyndicationFeed feed = null;
                try
                {
                    client.BypassCacheOnRetrieve = true;
                    feed = await client.RetrieveFeedAsync(uri);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);

                    int hr = ex.HResult;
                    int facility = (hr & 0x7FFF0000) / 0xFFFF;
                    int error = hr & 0xFFFF;
                    const int FACILITY_WIN32 = 7;
                    const int FACILITY_HTTP = 25;
                    const int NOT_FOUND = 404;

                    if (facility == FACILITY_HTTP && error == NOT_FOUND)
                    {
                        result.FileFound = false;
                        return result;
                    }
                    else if (facility == FACILITY_HTTP)
                    {
                        // Swallow HTTP errors. Treat them as file not found.
                        result.FileFound = false;
                        return result;
                    }
                    else if (facility == FACILITY_WIN32 && error > 12001 && error < 12156)
                    {
                        // Swallow WININET errors. Treat as file not found.
                        result.FileFound = false;
                        return result;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (QuestionsManager.AddQuestions(website, feed, skipLatestPubDate))
                {
                    result.Changed = true;
                }

                result.FileFound = true;
                return result;
            });
        }

        public static void UpdateTileAndBadge()
        {
            IList<BindableQuestion> list = QuestionsManager.GetSortedQuestions();
            UpdateTileWithQuestions(list);
            UpdateBadge(list.Count);
        }

        public static void UpdateTileWithQuestions(IList<BindableQuestion> list)
        {
            if (list.Count > 0)
            {
                int i = 0;
                foreach (BindableQuestion question in list)
                {
                    string message = question.Title;

#if DEBUG
                    // Append query time and question number.
                    message += " (" + DateTime.Now + ") (" + i + ")";
#endif

                    CreateTileUpdate(message);

                    // Limit tile updates.
                    i++;
                    if (i >= 5)
                    {
                        break;
                    }
                }
            }
        }

        // For more about tile templates:
        // http://msdn.microsoft.com/en-us/library/windows/apps/Hh761491.aspx
        public static void CreateTileUpdate(string text)
        {
            try
            {
                string tileXmlString = "<tile>"
                                     + "<visual>"
                                     + "<binding template='TileWideText04'>"
                                     + "<text id='1'>" + text + "</text>"
                                     + "</binding>"
                                     + "</visual>"
                                     + "</tile>";

                XmlDocument tileDOM = new XmlDocument();
                tileDOM.LoadXml(tileXmlString);
                TileNotification tile = new TileNotification(tileDOM);

                // Enable notification cycling.
                TileUpdateManager.CreateTileUpdaterForApplication().EnableNotificationQueue(true);

                TileUpdateManager.CreateTileUpdaterForApplication().Update(tile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static void UpdateBadge(int count)
        {
            XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
            badgeElement.SetAttribute("value", count.ToString());
            BadgeNotification badge = new BadgeNotification(badgeXml);
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);
        }

        public static void ClearTileAndBadge()
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
        }
    }
}
