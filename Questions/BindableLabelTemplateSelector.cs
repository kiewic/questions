using QuestionsBackgroundTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Questions
{
    class BindableLabelTemplateSelector : DataTemplateSelector
    {
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            BindableLabel label = item as BindableLabel;

            if (label.Type == BindableLabelType.BuzzWord)
            {
                return App.Current.Resources["BuzzWordTemplate"] as DataTemplate;
            }

            return App.Current.Resources["TagTemplate"] as DataTemplate;
        }
    }
}
