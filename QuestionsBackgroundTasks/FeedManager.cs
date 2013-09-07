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

        public static IAsyncOperation<bool> UpdateQuestions()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await ContentManager.LoadAsync();

                string query = ContentManager.ConcatenateAllTags();

                return await UpdateQuestions(query);
            });
        }

        public static IAsyncOperation<bool> UpdateQuestions(string query)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                await ContentManager.LoadAsync();

                ContentManager.ClearQuestions();

                if (String.IsNullOrEmpty(query))
                {
                    // There is nothing to query.
                    return true;
                }

                Uri uri;
                if (!ContentManager.TryCreateUri(query, out uri))
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
                    return false;
                }

                ContentManager.AddQuestions(feed);

                IList<BindableQuestion> list = ContentManager.GetSortedQuestions();
                UpdateTileWithQuestions(list);

                return true;
            });
        }

        public static void UpdateTileWithQuestions(IList<BindableQuestion> list)
        {
            if (list.Count > 2)
            {
                int i = 0;
                foreach (BindableQuestion question in list)
                {
                    string message = question.Title;

#if DEBUG
                    // Append query time and question number.
                    message += " (" + DateTime.Now + ") (" + i + ")";
#endif

                    UpdateTile(message);

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
        public static void UpdateTile(string text)
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
    }
}
