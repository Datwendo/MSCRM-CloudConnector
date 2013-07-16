using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xrm.Sdk;
using System.Net.Http;
using System.Web;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Runtime.Serialization.Json;
using Microsoft.Xrm.Sdk.Client;


namespace Datwendo.Crm.Sdk
{
    public class Connector
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public int PublisherId { get; set; }
        public int TransacKeyDelay { get; set; }
        public string SecretKey { get; set; }
        public bool IsFast { get; set; }
        public string ServiceUrl { get; set; }
        public string AssociatedEntity { get; set; }
        public string AssociatedAttribute { get; set; }
        public string Format { get; set; }

        public static Connector FindConnectors(IServiceProvider serviceProvider, IPluginExecutionContext context, string targetEntity)
        {
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            OrganizationServiceContext orgSvcContext = new OrganizationServiceContext(service);
            var query = from c in orgSvcContext.CreateQuery("dtw_connector")
                        where ((string)c["dtw_associatedentity"]).Contains(targetEntity)
                        select new Connector
                        {
                            Name                = c.Attributes["dtw_name"].ToString(),
                            Id                  = (int)c.Attributes["dtw_id"],
                            SecretKey           = c.Attributes["dtw_secretkey"].ToString(),
                            IsFast              = ((OptionSetValue)c.Attributes["dtw_protocol"]).Value == 100000000,
                            ServiceUrl          = c.Attributes["dtw_serviceurl"].ToString(),
                            PublisherId         = (int)c.Attributes["dtw_publisher"],
                            TransacKeyDelay     = (((int)c.Attributes["dtw_transactiondelay"]) > 100 ) ? (int)c.Attributes["dtw_transactiondelay"]:200,

                            AssociatedEntity    = targetEntity,
                            AssociatedAttribute = c.Attributes["dtw_associatedattribute"].ToString()
                        };
            return query.SingleOrDefault();
        }
    }
}
