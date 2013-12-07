using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Questions
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TagOptionsPage : Page
    {
        private BindableWebsite website;

        public TagOptionsPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            website = e.Parameter as BindableWebsite;
            WebsiteBlock.Text = website.Name;

            LoadingBar.ShowPaused = false;

            website.DisplayTags(TagsView);
            await OptionsManager.LoadAndDisplayTagOptionsAsync(TagOptionsView, website.ApiSiteParameter);

            LoadingBar.ShowPaused = true;
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            LoadingBar.ShowPaused = false;

            if (TagOptionsView.SelectedValue != null)
            {
                var tagOption = TagOptionsView.SelectedValue as BindableTagOption;
                string tag = tagOption.ToString();
                await ValidateAndAddTagAsync(tag);
            }
            else if (!String.IsNullOrWhiteSpace(TagBox.Text))
            {
                await ValidateAndAddTagAsync(TagBox.Text);
            }

            LoadingBar.ShowPaused = true;
        }

        private async Task ValidateAndAddTagAsync(string tag)
        {
            String tagEncoded = WebUtility.UrlEncode(tag.Trim());

            await QuestionsManager.LoadAsync();

            // Retrieve questions, skip the LatestPubDate validation and save all questions.
            QuerySingleWebsiteResult result = await FeedManager.QuerySingleWebsiteAsync(website.ToString(), tagEncoded, true);
            QuestionsManager.LimitTo150AndSave();

            if (result.FileFound)
            {
                website.AddTagAndSave(TagsView, tag);
                FeedManager.UpdateTileAndBadge();

                // Tag added, clear the test.
                TagBox.Text = "";
            }
            else
            {
                // Display red color while we figure it out a better way to diplay errors.
                TagBox.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagsView.SelectedValue != null)
            {
                website.DeleteTagAndSave(TagsView, TagsView.SelectedValue as string);
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void TagOptionsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TagBox.Text = "";
        }

        private void TagBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Bring back to black the color, in case we had an error before.
            TagBox.Foreground = new SolidColorBrush(Colors.Black);

            TagOptionsView.SelectedValue = null;
        }

        private void TagBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                AddButton_Click(null, null);
                e.Handled = true;
            }
        }
    }
}
