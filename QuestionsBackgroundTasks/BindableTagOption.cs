using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    public sealed class BindableTagOption
    {
        private JsonObject innerJsonObject;
        public BindableTagOption(JsonObject jsonObject)
        {
            this.innerJsonObject = jsonObject;
        }

        public override string ToString()
        {
            return innerJsonObject.GetNamedStringOrEmptyString("name");
        }

    }
}
