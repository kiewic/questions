using Questions.Common;
using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
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
        private const string taskName = "QuestionsTimerTask";
        private KeyEventHandler keyUpHandler;
        private TypedEventHandler<DataTransferManager, DataRequestedEventArgs> shareHandler;
        private BackgroundTaskCompletedEventHandler taskCompletedHandler;
        private TypedEventHandler<ApplicationData, object> dataChangedHandler;
        private IAsyncOperation<IUICommand> showOperation;

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
            RegisterShortcuts();
            RegisterForShare();
            RegisterDataChanged();

            // Load question into ListView for first time.
            UpdateQuestionsView(false);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            UnregisterDataChanged();
            UnregisterForShare();
            UnregisterShortcuts();
            UnregisterBackgroundTask();
        }

        private async void UpdateQuestionsView(bool forceQuery)
        {
            StatusBlock.Visibility = Visibility.Collapsed;

            await QuestionsManager.LoadAsync();

            IList<BindableQuestion> list = null;
            if (!forceQuery)
            {
                list = QuestionsManager.GetSortedQuestions();
            }

            if (list == null || list.Count == 0)
            {
                // Deleting sites or tags cleans the questions collection. Query again.
                LoadingBar.ShowPaused = false;
                await FeedManager.QueryWebsitesAsync();
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
            UnregisterBackgroundTask();

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
            builder.Name = taskName;
            builder.TaskEntryPoint = "QuestionsBackgroundTasks.TimerTask";
            builder.SetTrigger(new TimeTrigger(15, false));
            BackgroundTaskRegistration task = builder.Register();

            taskCompletedHandler = new BackgroundTaskCompletedEventHandler(TaskCompletedHandler);
            task.Completed += TaskCompletedHandler;
        }

        private void UnregisterBackgroundTask()
        {
            // Loop through all background tasks and unregister any that matches the givn name.
            foreach (var keyValuePair in BackgroundTaskRegistration.AllTasks)
            {
                IBackgroundTaskRegistration task = keyValuePair.Value;
                if (task.Name == taskName)
                {
                    task.Completed -= taskCompletedHandler;
                    task.Unregister(true);
                }
            }
        }

        private void TaskCompletedHandler(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            string message = sender.Name + " completed on " + DateTime.Now;
            Debug.WriteLine(message);

            #pragma warning disable 4014
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                #if DEBUG
                var dialog = new MessageDialog(message, "Task Completed");
                if (showOperation != null)
                {
                    showOperation.Cancel();
                }
                showOperation = dialog.ShowAsync();
                #endif

                // Unload is required, so the qeustions get uploaded from the file. It seems that the background trask and the app process
                // do not share the same QuestionManager static vars.
                QuestionsManager.Unload();

                // Do not force a query. This handler is called from the background task that just made a query.
                UpdateQuestionsView(false);
            });
            #pragma warning restore 4014
        }

        private void RegisterDataChanged()
        {
            dataChangedHandler = new TypedEventHandler<ApplicationData, object>(DataChangedHandler);
            ApplicationData.Current.DataChanged += dataChangedHandler;
        }

        private void UnregisterDataChanged()
        {
            ApplicationData.Current.DataChanged -= dataChangedHandler;
        }

        private async void DataChangedHandler(ApplicationData sender, object args)
        {
            try
            {
                string message = "Application data " + sender.Version + " synchronized on " + DateTime.Now;
                Debug.WriteLine(message);

                #if DEBUG
                var runOperation = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    var dialog = new MessageDialog(message, "Roaming Application Data Synchronized");
                    if (showOperation != null)
                    {
                        showOperation.Cancel();
                    }
                    showOperation = dialog.ShowAsync();
                });
                #endif

                SettingsManager.Unload();
                SettingsManager.Load();

                // Do a query. This handler is called when settings in another device change.
                // Sources maybe changed in the other device.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateQuestionsView(true));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void RegisterShortcuts()
        {
            // I don't know why, but assign the hanlder to the page does not work. But this does.
            keyUpHandler = new KeyEventHandler(Page_KeyUp);
            Window.Current.Content.AddHandler(UIElement.KeyUpEvent, keyUpHandler, false);
        }

        private void UnregisterShortcuts()
        {
            Window.Current.Content.RemoveHandler(UIElement.KeyUpEvent, keyUpHandler);
        }

        private void RegisterForShare()
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            shareHandler = new TypedEventHandler<DataTransferManager, DataRequestedEventArgs>(ShareHandler);
            dataTransferManager.DataRequested += shareHandler;
        }

        private void UnregisterForShare()
        {
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested -= shareHandler;
        }

        private void ShareHandler(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (QuestionsView.SelectedItem != null)
            {
                BindableQuestion question = QuestionsView.SelectedItem as BindableQuestion;

                DataRequest request = args.Request;
                request.Data.Properties.Title = question.Title;
                request.Data.Properties.Description = "Hey dude! I found this question using the Questions app and I think it may interest you.";
                request.Data.SetUri(question.Link);
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
                // Force a query, that's exactly why the user clicked this button.
                UpdateQuestionsView(true);
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

        private void QuestionsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuestionsView.SelectedItems.Count > 0)
            {
                MarkAllReadButton.Visibility = Visibility.Collapsed;
                MarkReadButton.Visibility = Visibility.Visible;
            }
            else
            {
                MarkAllReadButton.Visibility = Visibility.Visible;
                MarkReadButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            IList<BindableQuestion> list = QuestionsManager.GetSortedQuestions();
            if (list.Count == 0)
            {
                // There are no question, there is nothing to do.
                return;
            }

            // The first question is the most recent.
            DateTimeOffset newLastAllRead = list[0].PubDate;
            SettingsManager.LastAllRead = newLastAllRead;

            // Clear questions in the frontend and in the bsckend.
            QuestionsManager.ClearQuestions();
            QuestionsView.ItemsSource = null;

            await QuestionsManager.SaveAsync();

            FeedManager.ClearTileAndBadge();

            RefreshButton_Click(null, null);
        }

        private void MarkReadButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (BindableQuestion question in QuestionsView.SelectedItems)
            {
                QuestionsManager.RemoveQuestion(question.Id);
            }

            var saveOperation = QuestionsManager.SaveAsync();

            // Do not force a query. We are just removing some questions,
            // the list may have more questions.
            UpdateQuestionsView(false);
        }

        private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F5)
            {
                RefreshButton_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.C)
            {
                var ctrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (ctrlState != CoreVirtualKeyStates.None)
                {
                    Frame.Navigate(typeof(EasterEggPage));
                    e.Handled = true;
                }
            }
        }
    }
}
