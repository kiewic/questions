using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls;

namespace QuestionsBackgroundTasks
{
    public sealed class TimerTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

#if DEBUG
            InvokeSimpleToast("Hello!");
#endif

            try
            {
                await FeedManager.UpdateQuestions();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

#if DEBUG
            InvokeSimpleToast("There are new questions for you.");
#endif

            deferral.Complete();
        }

        void InvokeSimpleToast(string messageReceived)
        {
            // GetTemplateContent returns a Windows.Data.Xml.Dom.XmlDocument object containing
            // the toast XML
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            // You can use the methods from the XML document to specify all of the
            // required parameters for the toast
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            stringElements.Item(0).AppendChild(toastXml.CreateTextNode("Push notification message:"));
            stringElements.Item(1).AppendChild(toastXml.CreateTextNode(messageReceived));

            // Audio tags are not included by default, so must be added to the XML document.
            string audioSrc = "ms-winsoundevent:Notification.IM";
            XmlElement audioElement = toastXml.CreateElement("audio");
            audioElement.SetAttribute("src", audioSrc);

            IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            toastNode.AppendChild(audioElement);

            // Create a toast from the Xml, then create a ToastNotifier object to show the toast.
            ToastNotification toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }
}
