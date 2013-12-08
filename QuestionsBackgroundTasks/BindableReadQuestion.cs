using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    class BindableReadQuestion
    {
        private string readDateString;
        private string id;

        public BindableReadQuestion(string id, string readDateString)
        {
            this.id = id;
            this.readDateString = readDateString;
        }

        public string Id
        {
            get
            {
                return id;
            }
        }

        public DateTimeOffset ReadDate
        {
            get
            {
                return DateTimeOffset.Parse(readDateString);
            }
        }
    }
}
