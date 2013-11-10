﻿using System;
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

        public static IAsyncAction UpdateQuestions()
        {
            return UpdateQuestions(false);
        }

        // 1. Load settings.
        // 2. query -> Retrieve feed.
        // 3. skipLastAllRead -> Add questions.
        // 4. Sort Questions (for tile and badge).
        // 5. Update tile.
        // 6. Update badge.
        // TODO: Maybe buzz words check here.
        public static IAsyncAction UpdateQuestions(bool skipLastAllRead)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await SettingsManager.LoadAsync();
                await QuestionsManager.LoadAsync();

                // Query websites in parallel.
                List<Task> tasks = new List<Task>();
                foreach (string website in SettingsManager.GetWebsiteKeys())
                {
                    string query = SettingsManager.ConcatenateAllTags(website);
                    tasks.Add(UpdateQuestionsSingleWebsite(website, query, false).AsTask());
                }

                if (tasks.Count == 0)
                {
                    // There is nothing to wait.
                    return;
                }
                await Task.Factory.ContinueWhenAll(tasks.ToArray(), (tasks2) => {});

                QuestionsManager.LimitTo150AndSave();
                UpdateTileAndBadge();
            });
        }

        public static IAsyncOperation<bool> UpdateQuestionsSingleWebsite(string website, string query, bool skipLastAllRead)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                Uri uri;
                if (!SettingsManager.TryCreateUri(website, query, out uri))
                {
                    Debugger.Break();
                }

                SyndicationFeed feed = null;
                try
                {
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
                        return false; // File not found.
                    }
                    else if (facility == FACILITY_HTTP)
                    {
                        // Swallow HTTP errors.
                        return false; // Treat as file not found.
                    }
                    else if (facility == FACILITY_WIN32 && error > 12001 && error < 12156)
                    {
                        // Swallow WININET errors.
                        return false; // Treat as file not found.
                    }
                    else
                    {
                        throw;
                    }
                }

                QuestionsManager.AddQuestions(website, feed, skipLastAllRead);

                return true; // File found.
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
