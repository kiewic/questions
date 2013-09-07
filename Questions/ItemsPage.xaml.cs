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
    public sealed partial class ItemsPage : Page
    {
        public ItemsPage()
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
            RegisterBackgroundTask();
            LoadQuestions();
        }

        private async void LoadQuestions()
        {
            await ContentManager.LoadAsync();

            bool isEmpty = await ContentManager.IsEmptyAsync();
            if (isEmpty)
            {
                await FeedManager.UpdateQuestions();
            }

            IList<BindableQuestion> list = ContentManager.GetSortedQuestions();
            ContentManager.LoadQuestions(QuestionsView, list);
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
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => LoadQuestions());
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

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            bool result = await FeedManager.UpdateQuestions();
            if (result)
            {
                LoadQuestions();
            }
            else
            {
                // TODO: Display an error message.
            }
        }

        private async void QuestionsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            BindableQuestion question = e.ClickedItem as BindableQuestion;
            await Launcher.LaunchUriAsync(question.Link);
        }
    }
}
