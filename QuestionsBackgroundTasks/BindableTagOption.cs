using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    public sealed class BindableTagOption
    {
        private PowerpuffJsonObject json;
        public BindableTagOption(JsonObject jsonObject)
        {
            json = new PowerpuffJsonObject(jsonObject);
        }

        public override string ToString()
        {
            return json.GetNamedString("name");
        }

    }
}
