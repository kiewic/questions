using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
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
    public sealed partial class BuzzWordsPage : Page
    {
        private BindableWebsite website;

        public BuzzWordsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            website = e.Parameter as BindableWebsite;
            WebsiteBlock.Text = website.Name;

            website.DisplayBuzzWords(BuzzWordsView);
            //await OptionsManager.LoadAndDisplayTagOptionsAsync(TagOptionsView, website.ApiSiteParameter);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(BuzzWordBox.Text))
            {
                string buzzWord = BuzzWordBox.Text;

                website.AddBuzzWordAndSave(BuzzWordsView, buzzWord);

                // Buzz word added successfully, clear the text.
                BuzzWordBox.Text = "";
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (BuzzWordsView.SelectedValue != null)
            {
                website.DeleteBuzzWordAndSave(BuzzWordsView, BuzzWordsView.SelectedValue as string);
            }
        }

        private void BuzzWordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                AddButton_Click(null, null);
                e.Handled = true;
            }
        }

        private void BuzzWordsView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Delete)
            {
                RemoveButton_Click(null, null);
                BuzzWordsView.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
    }
}
