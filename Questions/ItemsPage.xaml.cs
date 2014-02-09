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

        #if DEBUG
        private IAsyncOperation<IUICommand> showOperation;
        #endif

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

            taskCompletedHandler = new BackgroundTaskCompletedEventHandler(OnTaskCompleted);
            task.Completed += OnTaskCompleted;
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

        private void RegisterDataChanged()
        {
            dataChangedHandler = new TypedEventHandler<ApplicationData, object>(OnDataChanged);
            ApplicationData.Current.DataChanged += dataChangedHandler;
        }

        private void UnregisterDataChanged()
        {
            ApplicationData.Current.DataChanged -= dataChangedHandler;
        }

        private void OnTaskCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            string message = sender.Name + " completed on " + DateTime.Now;
            string title = "Task Completed";

            // When a task completed, unload questions is required, so they get loaded from the file.
            // It seems that the background task and the app process do not share the same QuestionsManager
            // static vars.
            QuestionsManager.Unload();

            HandleTaskCompletedOrDataChanged(message, title);
        }

        private async void OnDataChanged(ApplicationData sender, object args)
        {
            string message = "Application data " + sender.Version + " synchronized on " + DateTime.Now;
            string title = "Roaming Application Data Synchronized";

            // When data changes. Settings should be unloaded/loaded so websites get synchronized.
            SettingsManager.Unload();
            SettingsManager.Load();

            // Also unload and load read-list.
            ReadListManager.Unload();

            // When data changes, the read-list may contain new read-questions.
            await QuestionsManager.RemoveReadQuestionsUpdateTileAndBadgeAndSaveAsync();

            HandleTaskCompletedOrDataChanged(message, title);
        }

        private async void HandleTaskCompletedOrDataChanged(string message, string title)
        {
            Debug.WriteLine(message);

            #if DEBUG
            var runOperation = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    var dialog = new MessageDialog(message, title);
                    if (showOperation != null)
                    {
                        showOperation.Cancel();
                    }
                    showOperation = dialog.ShowAsync();
                }
                catch (UnauthorizedAccessException ex)
                {
                    // E_ACCESSDENIED is expected here if the RequestAccessAsync operation is using the screen.
                    Debug.WriteLine(ex);
                }
            });
            #endif

            // Do not force a query when a task completed. This handler is called just after the background task
            // made a query.
            // Do not force a query when data changed. This handler is called when settings/questions in another device
            // change. Simply display the latest list of questions.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateQuestionsView(false));
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
            // Force a query, that's exactly why the user clicked this button.
            UpdateQuestionsView(true);
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

        private void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsView.Items.Count == 0)
            {
                // There are no question, there is nothing to do.
                return;
            }

            // Clear questions in the frontend and in the backend.
            QuestionsView.ItemsSource = null;
            QuestionsManager.RemoveQuestionsAndSave(null, null);

            FeedManager.ClearTileAndBadge();

            RefreshButton_Click(null, null);
        }

        private void MarkReadButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> keysToDelete = new List<string>();

            foreach (BindableQuestion question in QuestionsView.SelectedItems)
            {
                keysToDelete.Add(question.Id);
            }

            QuestionsManager.RemoveQuestionsAndSave(keysToDelete);

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
        }

        private void QuestionsView_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine((sender as ListView).Focus(FocusState.Keyboard));
        }
    }
}
