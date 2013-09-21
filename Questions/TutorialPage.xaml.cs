using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public sealed partial class TutorialPage : Page
    {
        private bool tutorialDone = false;

        public TutorialPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            int step = (int)(TutorialViewer.HorizontalOffset / TutorialViewer.Width);

            if (tutorialDone)
            {
                SkipButton_Click(sender, e);
                return;
            }

            // Move to next step.
            TutorialViewer.ScrollToHorizontalOffset(TutorialViewer.HorizontalOffset + TutorialViewer.Width);

            IsTutorailDone();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async void LearnMoreButton_Click(object sender, RoutedEventArgs e)
        {
            HyperlinkButton link = sender as HyperlinkButton;

            Uri helpUri = null;
            String uriString = "http://kiewic.com/questions/help";
            if (link == ContentButton)
            {
                helpUri = new Uri(uriString + "#content");
            }
            else
            {
                helpUri = new Uri(uriString + "#detailedstatus");
            }

            var result = await Windows.System.Launcher.LaunchUriAsync(helpUri);
        }

        private void TutorialViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            IsTutorailDone();
        }

        private void IsTutorailDone()
        {
            int step = (int)(TutorialViewer.HorizontalOffset / TutorialViewer.Width);

            // Is this the step 3 (last step)?
            if (step == 3)
            {
                NextButton.Content = "Done.";
                SkipButton.Visibility = Visibility.Collapsed;
                tutorialDone = true;
            }
        }
    }
}
