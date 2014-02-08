using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    class PowerpuffJsonObject
    {
        private JsonObject jsonObject;

        public PowerpuffJsonObject(JsonObject jsonObject)
        {
            this.jsonObject = jsonObject;
        }

        public bool ContainsKey(string key)
        {
            return jsonObject.ContainsKey(key);
        }

        public string GetNamedString(string name)
        {
            if (!jsonObject.ContainsKey(name))
            {
                return "";
            }
            string value = jsonObject.GetNamedString(name);

            // TODO: Decode all HTML entities.
            value = value.Replace("&amp;", "&");

            return value;
        }

        public JsonObject GetNamedObject(string name)
        {
            return jsonObject.GetNamedObject(name);
        }

        public JsonObject GetOrCreateNamedObject(string name)
        {
            if (!jsonObject.ContainsKey(name))
            {
                jsonObject.SetNamedValue(name, new JsonObject());
            }
            return jsonObject.GetNamedObject(name);
        }

        internal JsonArray GetOrCreateNamedArray(string name)
        {
            if (!jsonObject.ContainsKey(name))
            {
                jsonObject.SetNamedValue(name, new JsonArray());
            }
            return jsonObject.GetNamedArray(name);
        }

        public void SetNamedValue(string name, IJsonValue value)
        {
            jsonObject.SetNamedValue(name, value);
        }

        public string ConcatenateNamedObjectKeys(string name)
        {
            JsonObject childObject = jsonObject.GetNamedObject(name);
            return String.Join(", ", childObject.Keys);
        }

        public string ConcatenateNamedArrayStringValues(string name)
        {
            List<string> values = new List<string>();
            JsonArray childArray = GetOrCreateNamedArray(name);

            foreach (IJsonValue jsonValue in childArray)
            {
                if (jsonValue.ValueType == JsonValueType.String)
                {
                    string value = jsonValue.GetString();
                    values.Add(value);
                }
            }

            return String.Join(", ", values.ToArray());
        }

        public JsonObject ToJsonObject()
        {
            return jsonObject;
        }
    }
}
