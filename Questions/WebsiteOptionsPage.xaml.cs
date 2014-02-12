using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
    public sealed partial class WebsiteOptionsPage : Page
    {
        public WebsiteOptionsPage()
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
            LoadingBar.ShowPaused = false;

            await OptionsManager.LoadAndDisplayWebsitesAsync(WebsiteOptionsView);

            LoadingBar.ShowPaused = true;
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebsiteOptionsView.SelectedItem != null)
            {
                BindableWebsiteOption websiteOption = WebsiteOptionsView.SelectedItem as BindableWebsiteOption;
                BindableWebsite website = await SettingsManager.AddWebsiteAndSave(websiteOption);

                if (website != null)
                {
                    Frame.Navigate(typeof(TagOptionsPage), website);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}
