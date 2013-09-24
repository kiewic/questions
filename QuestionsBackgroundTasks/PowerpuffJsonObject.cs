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

        public string ConcatenateNamedObjectKeys(string name)
        {
            JsonObject childObject = jsonObject.GetNamedObject(name);
            return String.Join(", ", childObject.Keys);
        }
    }
}
