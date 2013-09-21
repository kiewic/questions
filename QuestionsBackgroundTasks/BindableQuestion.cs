using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    public sealed class BindableQuestion
    {
        private JsonObject jsonObject;

        public BindableQuestion(JsonObject jsonObject)
        {
            this.jsonObject = jsonObject;
        }

        public string Title
        {
            get
            {
                return jsonObject.GetNamedString("Title");
            }
        }

        public DateTimeOffset PubDate
        {
            get
            {
                return DateTimeOffset.Parse(jsonObject.GetNamedString("PubDate"));
            }
        }

        public Uri Link
        {
            get
            {
                return new Uri(jsonObject.GetNamedString("Link"));
            }
        }

        public string PubDateDiff
        {
            get
            {
                TimeSpan diff = DateTime.Now - PubDate;
                if (diff.TotalSeconds < 60)
                {
                    return (int)diff.TotalSeconds + " seconds ago";
                }
                else if (diff.TotalMinutes < 60)
                {
                    return (int)diff.TotalMinutes + " minutes ago";
                }
                else if (diff.TotalHours < 24)
                {
                    return (int)diff.TotalHours + " hours ago";
                }

                return (int)diff.TotalDays + " days ago";
            }
        }
    }
}
