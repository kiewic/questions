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
        private const string TagsKey = "Tags";
        private const string BuzzWordsKey = "BuzzWords";

        string id;
        JsonObject roamingJsonObject;

        public BindableWebsite(string id, JsonObject roamingJsonObject)
        {
            this.id = id;
            this.roamingJsonObject = roamingJsonObject;
        }

        public override string ToString()
        {
            return id;
        }

        public void AddTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = roamingJsonObject.GetNamedObject(TagsKey);

            if (tagsCollection.ContainsKey(tag))
            {
                // We already have this tag.
                Debug.WriteLine("Tag repeated: {0}", tag);
                return;
            }

            JsonValue nullValue = JsonValue.Parse("null");
            tagsCollection.Add(tag, nullValue);
            listView.Items.Add(tag);

            SettingsManager.SaveRoaming();
        }

        public void AddBuzzWordAndSave(ListView listView, string buzzWord)
        {
            JsonArray buzzWordsCollection = roamingJsonObject.GetOrCreateNamedArray(BuzzWordsKey);

            IJsonValue selectedValue;
            if (buzzWordsCollection.ContainsStringValue(buzzWord, out selectedValue))
            {
                // We already have this buzz word.
                Debug.WriteLine("Buzz word repeated: {0}", buzzWord);
                return;
            }

            buzzWordsCollection.Add(JsonValue.CreateStringValue(buzzWord));
            listView.Items.Add(buzzWord);

            SettingsManager.SaveRoaming();
        }

        public void DeleteTagAndSave(ListView listView, string tag)
        {
            JsonObject tagsCollection = roamingJsonObject.GetNamedObject(TagsKey);

            if (tagsCollection.Remove(tag))
            {
                SettingsManager.SaveRoaming();
            }

            listView.Items.Remove(tag);

            // Remove only questions containing this website and this tag.
            QuestionsManager.RemoveQuestionsAndSave(id, tag);
        }

        public void DeleteBuzzWordAndSave(ListView listView, string buzzWord)
        {
            JsonArray buzzWordsCollection = roamingJsonObject.GetOrCreateNamedArray(BuzzWordsKey);

            IJsonValue selectedValue;
            if (buzzWordsCollection.ContainsStringValue(buzzWord, out selectedValue))
            {
                buzzWordsCollection.Remove(selectedValue);

                SettingsManager.SaveRoaming();
            }

            listView.Items.Remove(buzzWord);
        }

        public void DisplayTags(ListView listView)
        {
            JsonObject tagsCollection = roamingJsonObject.GetNamedObject(TagsKey);

            foreach (string tag in tagsCollection.Keys)
            {
                listView.Items.Add(tag);
            }
        }

        public void DisplayBuzzWords(ListView listView)
        {
            JsonArray buzzWordsCollection = roamingJsonObject.GetOrCreateNamedArray(BuzzWordsKey);

            foreach (IJsonValue jsonValue in buzzWordsCollection)
            {
                string buzzWord = jsonValue.GetString();
                listView.Items.Add(buzzWord);
            }
        }

        public string ApiSiteParameter
        {
            get
            {
                return roamingJsonObject.GetNamedStringOrEmptyString("ApiSiteParameter");
            }
        }

        public string Name
        {
            get
            {
                return roamingJsonObject.GetNamedStringOrEmptyString("Name");
            }
        }

        public string IconUrl
        {
            get
            {
                string iconUrl = roamingJsonObject.GetNamedStringOrEmptyString("IconUrl");
                return iconUrl;
            }
        }

        public string FaviconUrl
        {
            get
            {
                return roamingJsonObject.GetNamedStringOrEmptyString("FaviconUrl");
            }
        }

        public string Tags
        {
            get
            {
                JsonObject jsonObject = roamingJsonObject.GetNamedObject(TagsKey);
                return jsonObject.ConcatenateKeys();
            }
        }

        public string BuzzWords
        {
            get
            {
                JsonArray jsonArray = roamingJsonObject.GetOrCreateNamedArray(BuzzWordsKey);
                return jsonArray.ConcatenateStringValues();
            }
        }

    }
}
