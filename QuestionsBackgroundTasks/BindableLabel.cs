using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuestionsBackgroundTasks
{
    public enum BindableLabelType
    {
        BuzzWord = 1,
        Tag
    }

    public sealed class BindableLabel
    {
        public string Value
        {
            get;
            set;
        }

        public BindableLabelType Type
        {
            get;
            set;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
