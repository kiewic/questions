using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml.Controls;

namespace QuestionsBackgroundTasks
{
    public sealed class BindableWebsite
    {
        PowerpuffJsonObject json;

        public BindableWebsite(JsonObject jsonObject)
        {
            json = new PowerpuffJsonObject(jsonObject);
        }

        public override string ToString()
        {
            return json.GetNamedString("SiteUrl");
        }

        public async void AddTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = json.GetNamedObject("Tags");

            if (tagsCollection.ContainsKey(tag))
            {
                // We already have this tag.
                Debug.WriteLine("Tag repeated: {0}", tag);
                return;
            }

            JsonValue nullValue = JsonValue.Parse("null");
            tagsCollection.Add(tag, nullValue);
            listView.Items.Add(tag);

            await ContentManager.SaveAsync();
        }

        public async void DeleteTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = json.GetNamedObject("Tags");

            tagsCollection.Remove(tag);
            listView.Items.Remove(tag);

            // TODO: Remove only questions containing this tag.
            QuestionsManager.ClearQuestions();

            await ContentManager.SaveAsync();
        }

        public void DisplayTags(ListView listView)
        {
            JsonObject tagsCollection = json.GetNamedObject("Tags");

            foreach (string tag in tagsCollection.Keys)
            {
                listView.Items.Add(tag);
            }
        }

        public string ApiSiteParameter
        {
            get
            {
                return json.GetNamedString("ApiSiteParameter");
            }
        }

        public string Name
        {
            get
            {
                return json.GetNamedString("Name");
            }
        }

        public string IconUrl
        {
            get
            {
                string iconUrl = json.GetNamedString("IconUrl");
                return iconUrl;
            }
        }

        public string FaviconUrl
        {
            get
            {
                return json.GetNamedString("FaviconUrl");
            }
        }

        public string Tags
        {
            get
            {
                return json.ConcatenateNamedObjectKeys("Tags");
            }
        }
    }
}
