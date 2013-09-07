using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Syndication;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Questions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SyndicationClient client = new SyndicationClient();

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summry>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ContentManager.LoadTags(TagsView);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            String tag = TagBox.Text.Trim();
            String tagEncoded = WebUtility.UrlEncode(tag);
            Uri uri;
            if (!ContentManager.TryCreateUri(tagEncoded, out uri))
            {
                SetStatus("Invalid URI.", false);
                return;
            }

            SyndicationFeed feed = null;
            try
            {
                feed = await client.RetrieveFeedAsync(uri);

                Debug.WriteLine(feed.Title.Text);

                String successMessage = "Great! Now, we will keep you updated with " + tag + " questions.";

                if (feed.Items.Count > 0)
                {
                    successMessage += " E.g.: " + feed.Items[0].Title.Text;
                    FeedManager.UpdateTile(feed.Items[0].Title.Text);
                }

                SetStatus(successMessage, true);

                ContentManager.AddTag(TagsView, tag);
                ContentManager.AddQuestions(feed);
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2145844844)
                {
                    SetStatus("That tag does not exist. Try a different tag, e.g.: windows-runtime", false);
                }
                else
                {
                    SetStatus(ex.Message, false);
                }
            }
        }

        private void SetStatus(string text, bool success)
        {
            if (String.IsNullOrEmpty(text))
            {
                StatusBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusBlock.Visibility = Visibility.Visible;
                StatusBlock.Text = text;
            }

            if (success)
            {
                StatusBlock.Style = Resources["SuccessBlock"] as Style;
                StatusBorder.Style = Resources["SuccessBorder"] as Style;
            }
            else
            {
                StatusBlock.Style = Resources["ErrorBlock"] as Style;
                StatusBorder.Style = Resources["ErrorBorder"] as Style;
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ItemsPage));
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> tags = new List<string>();
            foreach (object selectedItem in TagsView.SelectedItems)
            {
                tags.Add(selectedItem as string);
            }

            foreach (string tag in tags)
            {
                ContentManager.DeleteTag(TagsView, tag);
            }
        }

    }
}
