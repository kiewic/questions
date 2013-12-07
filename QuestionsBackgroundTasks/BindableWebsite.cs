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
        string id;
        PowerpuffJsonObject roamingJson;

        public BindableWebsite(string id, JsonObject roamingJsonObject)
        {
            this.id = id;
            roamingJson = new PowerpuffJsonObject(roamingJsonObject);
        }

        public override string ToString()
        {
            return id;
        }

        public void AddTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = roamingJson.GetNamedObject("Tags");

            if (tagsCollection.ContainsKey(tag))
            {
                // We already have this tag.
                Debug.WriteLine("Tag repeated: {0}", tag);
                return;
            }

            JsonValue nullValue = JsonValue.Parse("null");
            tagsCollection.Add(tag, nullValue);
            listView.Items.Add(tag);

            SettingsManager.Save();
        }

        public void DeleteTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = roamingJson.GetNamedObject("Tags");

            tagsCollection.Remove(tag);
            listView.Items.Remove(tag);

            // Remove only questions containing this website and this tag. Then save, do not wait
            // until save is completed.
            QuestionsManager.RemoveQuestions(id, tag);
            var saveOperation = QuestionsManager.SaveAsync();

            SettingsManager.Save();
        }

        public void DisplayTags(ListView listView)
        {
            JsonObject tagsCollection = roamingJson.GetNamedObject("Tags");

            foreach (string tag in tagsCollection.Keys)
            {
                listView.Items.Add(tag);
            }
        }

        public string ApiSiteParameter
        {
            get
            {
                return roamingJson.GetNamedString("ApiSiteParameter");
            }
        }

        public string Name
        {
            get
            {
                return roamingJson.GetNamedString("Name");
            }
        }

        public string IconUrl
        {
            get
            {
                string iconUrl = roamingJson.GetNamedString("IconUrl");
                return iconUrl;
            }
        }

        public string FaviconUrl
        {
            get
            {
                return roamingJson.GetNamedString("FaviconUrl");
            }
        }

        public string Tags
        {
            get
            {
                return roamingJson.ConcatenateNamedObjectKeys("Tags");
            }
        }
    }
}
