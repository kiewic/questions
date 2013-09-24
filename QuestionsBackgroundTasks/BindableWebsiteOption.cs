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
        private PowerpuffJsonObject json;

        public BindableWebsiteOption(JsonObject jsonObject)
        {
            json = new PowerpuffJsonObject(jsonObject);
        }

        public override string ToString()
        {
            return json.GetNamedString("name");
        }

        public bool IsListable
        {
            get
            {
                if (json.GetNamedString("site_type") == "meta_site")
                {
                    return false;
                }

                if (json.GetNamedString("site_state") == "closed_beta")
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
                return json.GetNamedString("api_site_parameter");
            }
        }

        public string SiteUrl
        {
            get
            {
                return json.GetNamedString("site_url");
            }
        }

        public string IconUrl
        {
            get{
                return json.GetNamedString("icon_url");
            }
        }

        public string FaviconUrl
        {
            get
            {
                return json.GetNamedString("favicon_url");
            }
        }

        public string Audience
        {
            get
            {
                return json.GetNamedString("audience");
            }
        }

    }
}
