using Questions.Common;
using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
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
    public sealed partial class ItemsPage : LayoutAwarePage
    {
        public ItemsPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            RegisterBackgroundTask();

            DisplayOrUpdateQuestions(false);
        }

        private async void DisplayOrUpdateQuestions(bool forceUpdate)
        {
            await QuestionsManager.LoadAsync();

            IList<BindableQuestion> list = null;
            if (!forceUpdate)
            {
                list = QuestionsManager.GetSortedQuestions();
            }

            if (list == null || list.Count == 0)
            {
                // Deleting sites or tags cleans the questions collection. Query again.
                StatusBlock.Visibility = Visibility.Collapsed;
                LoadingBar.ShowPaused = false;
                await FeedManager.UpdateQuestions();
                LoadingBar.ShowPaused = true;

                list = QuestionsManager.GetSortedQuestions();

                if (list.Count == 0)
                {
                    StatusBlock.Visibility = Visibility.Visible;
                }
            }

            QuestionsManager.DisplayQuestions(QuestionsView, list);
        }

        private async void RegisterBackgroundTask()
        {
            const string name = "QuestionsTimerTask";
            UnregisterBackgroundTask(name);

            try
            {
                BackgroundAccessStatus status = await BackgroundExecutionManager.RequestAccessAsync();
                Debug.WriteLine("BackgroundAccessStatus: " + status);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            // A background task can be registered even if lock screen access was denied.
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
            builder.Name = name;
            builder.TaskEntryPoint = "QuestionsBackgroundTasks.TimerTask";
            builder.SetTrigger(new TimeTrigger(15, false));
            BackgroundTaskRegistration task = builder.Register();

            task.Completed += (sender, args) =>
            {
                Debug.WriteLine(args.InstanceId + " completed on " + DateTime.Now);
#pragma warning disable 4014
                // Do not force Update, this method is called just from the backgroun task that updated everything.
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => DisplayOrUpdateQuestions(false));
#pragma warning restore 4014
            };
        }

        private void UnregisterBackgroundTask(string name)
        {
            // Loop through all background tasks and unregister any that matches the givn name.
            foreach (var keyValuePair in BackgroundTaskRegistration.AllTasks)
            {
                IBackgroundTaskRegistration task = keyValuePair.Value;
                if (task.Name == name)
                {
                    task.Unregister(true);
                }
            }
        }

        private void TagsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisplayOrUpdateQuestions(true);
            }
            catch (Exception ex)
            {
                // TODO: Display an error message.
                Debug.WriteLine(ex);
            }
        }

        private async void QuestionsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            BindableQuestion question = e.ClickedItem as BindableQuestion;
            await Launcher.LaunchUriAsync(question.Link);
        }

        private async void AllReadButton_Click(object sender, RoutedEventArgs e)
        {
            IList<BindableQuestion> list = QuestionsManager.GetSortedQuestions();
            if (list.Count == 0)
            {
                // There are no question, there is nothing to do.
                return;
            }

            // The first question is the most recent.
            DateTimeOffset newLastAllRead = list[0].PubDate;
            QuestionsManager.LastAllRead = newLastAllRead;

            // Clear questions in the frontend and in the bsckend.
            QuestionsManager.ClearQuestions();
            QuestionsView.ItemsSource = null;

            await QuestionsManager.SaveAsync();

            FeedManager.ClearTileAndBadge();

            RefreshButton_Click(null, null);
        }
    }
}
