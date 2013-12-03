using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuestionsBackgroundTasks
{
    public sealed class QuerySingleWebsiteResult
    {
        public QuerySingleWebsiteResult()
        {
            // We don't have any reason to think the file was not found.
            FileFound = true;

            // Assume nothing has changed. If nothing has changed, we will be able to conserve resoureces.
            Changed = false;
        }

        public bool FileFound { get; set; }
        public bool Changed { get; set; }
    }
}
