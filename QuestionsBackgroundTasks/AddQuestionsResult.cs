using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuestionsBackgroundTasks
{
    public sealed class AddQuestionsResult
    {
        public AddQuestionsResult()
        {
            FileFound = true;
        }

        public bool FileFound { get; set; }
        public uint AddedQuestions { get; set; }
        public uint UpdatedQuestions { get; set; }
    }
}
