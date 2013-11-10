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
            SettingsManager.LoadAndDisplayWebsites(WebsitesView);
        }

        private void WebsitesView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WebsitesView.SelectedItem != null)
            {
                EditTagsButton.Visibility = Visibility.Visible;
                DeleteSiteButton.Visibility = Visibility.Visible;
            }
            else
            {
                EditTagsButton.Visibility = Visibility.Collapsed;
                DeleteSiteButton.Visibility = Visibility.Collapsed;
            }
        }

        private void AddSiteButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WebsiteOptionsPage));
        }

        private void EditTagsButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebsitesView.SelectedItem == null)
            {
                throw new Exception("No website selected.");
            }
            Frame.Navigate(typeof(TagOptionsPage), WebsitesView.SelectedItem);
        }

        private void DeleteSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebsitesView.SelectedItem == null)
            {
                throw new Exception("No website selected.");
            }
            SettingsManager.DeleteWebsiteAndSave(WebsitesView.SelectedItem as BindableWebsite);
            SettingsManager.LoadAndDisplayWebsites(WebsitesView);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ItemsPage));
        }
    }
}
