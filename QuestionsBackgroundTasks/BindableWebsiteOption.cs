using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace QuestionsBackgroundTasks
{
    public sealed class BindableWebsiteOption
    {
        private JsonObject innerJsonObject;

        public BindableWebsiteOption(JsonObject jsonObject)
        {
            this.innerJsonObject = jsonObject;
        }

        public override string ToString()
        {
            return innerJsonObject.GetNamedStringOrEmptyString("name");
        }

        public bool IsListable
        {
            get
            {
                if (innerJsonObject.GetNamedStringOrEmptyString("site_type") == "meta_site")
                {
                    return false;
                }

                if (innerJsonObject.GetNamedStringOrEmptyString("site_state") == "closed_beta")
                {
                    return false;
                }

                return true;
            }
        }

        public string ApiSiteParameter
        {
            get
            {
                return innerJsonObject.GetNamedStringOrEmptyString("api_site_parameter");
            }
        }

        public string SiteUrl
        {
            get
            {
                return innerJsonObject.GetNamedStringOrEmptyString("site_url");
            }
        }

        public string IconUrl
        {
            get{
                return innerJsonObject.GetNamedStringOrEmptyString("icon_url");
            }
        }

        public string FaviconUrl
        {
            get
            {
                return innerJsonObject.GetNamedStringOrEmptyString("favicon_url");
            }
        }

        public string Audience
        {
            get
            {
                return innerJsonObject.GetNamedStringOrEmptyString("audience");
            }
        }

    }
}
