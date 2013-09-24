using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagOptionsView.SelectedValue != null)
            {
                website.AddTagAndSave(TagsView, TagOptionsView.SelectedValue as BindableTagOption);
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
    }
}
