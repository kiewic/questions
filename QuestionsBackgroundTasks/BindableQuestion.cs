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
        private PowerpuffJsonObject json;
        private string id;

        public BindableQuestion(string id, JsonObject jsonObject)
        {
            this.id = id;
            json = new PowerpuffJsonObject(jsonObject);
        }

        public JsonObject ToJsonObject()
        {
            return json.ToJsonObject();
        }

        public string Id
        {
            get
            {
                return id;
            }
        }

        public string Website
        {
            get
            {
                return json.GetNamedString("Website");
            }
        }

        public string Title
        {
            get
            {
                return json.GetNamedString("Title");
            }
        }

        public DateTimeOffset PubDate
        {
            get
            {
                return DateTimeOffset.Parse(json.GetNamedString("PubDate"));
            }
        }

        public Uri Link
        {
            get
            {
                return new Uri(json.GetNamedString("Link"));
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
