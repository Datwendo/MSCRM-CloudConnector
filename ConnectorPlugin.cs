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
    /// <summary>
    /// Datwendo sandboxed plug-in
    /// </summary>
    public sealed class ConnectorPlugin : IPlugin
    {                
        private const string CCtorAPIController = "CCtor";
        private const string DataCCtorAPIController = "DataCCtor";
        private const string BlobCCtorAPIController = "BlobCCtor";


        #region  Base Requests Structures

        public class CCtrRequest
        {
            public string Ky { get; set; }
        }

        public class CCtrResponse
        {
            public int Cd { get; set; }
            public int Vl { get; set; }
        }

        public class CCtrRequest2 : CCtrRequest
        {
            public int Dl { get; set; }
        }

        // With publisher for Put
        public class PubCCtrRequest : CCtrRequest
        {
            public int Pb { get; set; }
        }

        public class StringStorRequest : PubCCtrRequest
        {
            public string St { get; set; }
        }


        public class CCtrResponse2
        {
            public int Cd { get; set; }
            public int Pr { get; set; }
            public string Ky { get; set; }
        }

        #endregion // Base Requests

        #region WebAPI Calls

        // Extract a new transaction key from server
        public bool TransacKey(ITracingService tracingService, Connector connector, out string NewKey)
        {
            tracingService.Trace("ConnectorPlugin: TransacKey BEG.");
            bool ret                                = false;
            NewKey                                  = string.Empty;
            CCtrRequest2 CParam = new CCtrRequest2
            {
                Ky = connector.SecretKey,
                Dl = connector.TransacKeyDelay
            };

            tracingService.Trace("ConnectorPlugin: TransacKey Ky: {0}, Dl: {1}", CParam.Ky, CParam.Dl);
            try
            {
                Stream str = Post4TransacSync(tracingService, connector, CParam);

                DataContractJsonSerializer json2    = new DataContractJsonSerializer(typeof(CCtrResponse2));
                CCtrResponse2 CRep                  = (CCtrResponse2)json2.ReadObject(str);
                tracingService.Trace("ConnectorPlugin: TransacKey CRep.Cd: {0}", CRep.Cd);
                if (CRep.Cd == 0)
                {
                    ret                             = true;
                    NewKey                          = CRep.Ky;
                    tracingService.Trace("ConnectorPlugin: TransacKey NewKey: {0}", NewKey);
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: ex: {0}, TransacKey {1} - {2}", new Object[] { ex.Message, connector.SecretKey, connector.Id });
                ret                                 = false;
            }
            tracingService.Trace("ConnectorPlugin: TransacKey END ret {0}", ret);
            return ret;
        }

        private Stream Post4TransacSync(ITracingService tracingService, Connector connector, CCtrRequest2 CReq)
        {
            Stream resultStream                     = null;
            try
            {
                Uri address                         = new Uri(string.Format("{0}/{1}/{2}", connector.ServiceUrl, CCtorAPIController, connector.Id));
                DataContractJsonSerializer jsonSer  = new DataContractJsonSerializer(typeof(CCtrRequest2));
                MemoryStream ms                     = new MemoryStream();
                jsonSer.WriteObject(ms, CReq);
                ms.Position                         = 0;
                StreamReader sr                     = new StreamReader(ms);

                //Post the data 
                var httpWebRequest                  = (HttpWebRequest)WebRequest.Create(address);
                httpWebRequest.Method               = "POST";
                httpWebRequest.ContentType          = "application/json";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(sr.ReadToEnd());
                    streamWriter.Flush();
                    streamWriter.Close();

                    var response                    = (HttpWebResponse)httpWebRequest.GetResponse();
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                        case HttpStatusCode.Accepted:
                        {
                            resultStream            = response.GetResponseStream();
                            break;
                        }
                        default:
                        {
                            tracingService.Trace(string.Format("ConnectorPlugin: Post4TransacSync HTTP Status: {0} - Reason: {1}", response.StatusCode, response.StatusDescription));
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: Post4TransacSync  ex: {0}, Error reading from WebAPI", ex);
                throw;
            }
            return resultStream;
        }
        
        
        // Read the actual value for Connector
        public bool Read(ITracingService tracingService,Connector connector,out int Val)
        {
            tracingService.Trace("ConnectorPlugin: Read BEG.");
            
            bool ret                                = false;
            Val                                     = int.MinValue;

            string NewKey = connector.SecretKey;
            if (!connector.IsFast && !TransacKey(tracingService,connector, out NewKey))
                return false;

            string ky                               = HttpUtility.UrlEncode(NewKey);

            try
            {
                Stream str                          = GetSync(tracingService, connector, ky);
                DataContractJsonSerializer json2    = new DataContractJsonSerializer(typeof(CCtrResponse));
                CCtrResponse CRep                   = (CCtrResponse)json2.ReadObject(str);
                tracingService.Trace("ConnectorPlugin: Read CRep.Cd: {0}", CRep.Cd);
                if (CRep.Cd == 0)
                {
                    ret                             = true;
                    Val                             = CRep.Vl;
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: Read ex: {0}, TransacKey {1} - {2}", new Object[] { ex.Message, connector.SecretKey, connector.Id });
                return false;
            }
            return ret;
        }

        private Stream GetSync(ITracingService tracingService,Connector connector,string Ky)
        {
            Stream resultStream                     = null;
            try
            {
                Uri address                         = new Uri(string.Format("{0}/{1}/{2}?Ky={3}", connector.ServiceUrl, CCtorAPIController, connector.Id, Ky));

                var httpWebRequest                  = (HttpWebRequest)WebRequest.Create(address);
                httpWebRequest.ContentType          = "text/json";
                httpWebRequest.Method               = "GET";

                using (var streamWriter             = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Flush();
                    streamWriter.Close();

                    var response                    = (HttpWebResponse)httpWebRequest.GetResponse();
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                        case HttpStatusCode.Accepted:
                        {
                            resultStream = response.GetResponseStream();
                            break;
                        }
                        default:
                        {
                            tracingService.Trace("ConnectorPlugin: GetSync HTTP Status: {0} - Reason: {1}", response.StatusCode, response.StatusDescription);
                            break;
                        }
                    }
                }
             }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: GetAsync ex: {0}, Error reading from WebAPI", ex);
                throw;
            }
            return resultStream;
        }
        

        public bool ReadNext(ITracingService tracingService,Connector connector,out int newVal)
        {
            tracingService.Trace("ConnectorPlugin: ReadNext BEG.");
            bool ret                                = false;
            newVal                                  = int.MinValue;

            string NewKey                           = connector.SecretKey;
            if (!connector.IsFast && !TransacKey(tracingService,connector, out NewKey))
                return false;

            PubCCtrRequest CReq                     = new PubCCtrRequest
            {
                Ky                                  = NewKey,
                Pb                                  = connector.PublisherId
            };

            try
            {
                Stream str                          = PutSync(tracingService, connector, CReq);
                DataContractJsonSerializer json2    = new DataContractJsonSerializer(typeof(CCtrResponse));
                CCtrResponse CRep                   = (CCtrResponse)json2.ReadObject(str);
                tracingService.Trace("ConnectorPlugin: ReadNext CRep.Cd: {0}", CRep.Cd);
                if (CRep.Cd == 0)
                {
                    newVal                          = CRep.Vl;
                    ret                             = true;
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: ex: {0}, ReadNext {1} - {2}", new Object[] { ex.Message, connector.SecretKey, connector.Id });
                ret                                 = false;
            }
            tracingService.Trace("ConnectorPlugin: ReadNext END : {0}", ret);
            return ret;
        }

        private Stream PutSync(ITracingService tracingService,Connector connector, PubCCtrRequest CReq)
        {
            Stream resultStream                     = null;
            try
            {
                Uri address                         = new Uri(string.Format("{0}/{1}/{2}", connector.ServiceUrl, CCtorAPIController, connector.Id));
                DataContractJsonSerializer jsonSer  = new DataContractJsonSerializer(typeof(PubCCtrRequest));
                MemoryStream ms                     = new MemoryStream();
                jsonSer.WriteObject(ms, CReq);
                ms.Position                         = 0;

                StreamReader sr                     = new StreamReader(ms);
                var httpWebRequest                  = (HttpWebRequest)WebRequest.Create(address);
                httpWebRequest.ContentType          = "text/json";
                httpWebRequest.Method               = "PUT";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(sr.ReadToEnd());
                    streamWriter.Flush();
                    streamWriter.Close();

                    var response                    = (HttpWebResponse)httpWebRequest.GetResponse();
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                        case HttpStatusCode.Accepted:
                        {
                            resultStream            = response.GetResponseStream();
                            break;
                        }
                        default:
                        {
                            tracingService.Trace("ConnectorPlugin: PutSync HTTP Status: {0} - Reason: {1}", response.StatusCode, response.StatusDescription);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: PutSync  ex: {0}, Error reading from WebAPI", ex);
                throw;
            }
            return resultStream;
        }

        public bool ReadNextWithData(ITracingService tracingService, Connector connector, string strVal, out int newVal)
        {
            tracingService.Trace("ConnectorPlugin: ReadNextWithData BEG.");
            bool ret        = false;
            newVal          = int.MinValue;

            string NewKey   = connector.SecretKey;
            if (!connector.IsFast && !TransacKey(tracingService, connector, out NewKey))
                return false;

            StringStorRequest CReq  = new StringStorRequest
            {
                Ky                  = NewKey,
                Pb                  = connector.PublisherId,
                St                  = strVal
            };

            try
            {
                Stream str                          = PutSyncWithData(tracingService, connector, CReq);
                DataContractJsonSerializer json2    = new DataContractJsonSerializer(typeof(CCtrResponse));
                CCtrResponse CRep                   = (CCtrResponse)json2.ReadObject(str);
                tracingService.Trace("ConnectorPlugin: ReadNextWithData CRep.Cd: {0}", CRep.Cd);
                if (CRep.Cd == 0)
                {
                    newVal                          = CRep.Vl;
                    ret                             = true;
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: ex: {0}, ReadNextWithData {1} - {2}", new Object[] { ex.Message, connector.SecretKey, connector.Id });
                ret = false;
            }
            tracingService.Trace("ConnectorPlugin: ReadNextWithData END : {0}", ret);
            return ret;
        }

        private Stream PutSyncWithData(ITracingService tracingService, Connector connector, StringStorRequest CReq)
        {
            Stream resultStream = null;
            try
            {
                Uri address                         = new Uri(string.Format("{0}/{1}/{2}", connector.ServiceUrl, DataCCtorAPIController, connector.Id));
                DataContractJsonSerializer jsonSer  = new DataContractJsonSerializer(typeof(StringStorRequest));
                MemoryStream ms                     = new MemoryStream();
                jsonSer.WriteObject(ms, CReq);
                ms.Position                         = 0;

                StreamReader sr                     = new StreamReader(ms);
                var httpWebRequest                  = (HttpWebRequest)WebRequest.Create(address);
                httpWebRequest.ContentType          = "text/json";
                httpWebRequest.Method               = "PUT";

                using (var streamWriter             = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(sr.ReadToEnd());
                    streamWriter.Flush();
                    streamWriter.Close();

                    var response                    = (HttpWebResponse)httpWebRequest.GetResponse();
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                        case HttpStatusCode.Accepted:
                            {
                                resultStream        = response.GetResponseStream();
                                break;
                            }
                        default:
                            {
                                tracingService.Trace("ConnectorPlugin: PutSyncWithData HTTP Status: {0} - Reason: {1}", response.StatusCode, response.StatusDescription);
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ConnectorPlugin: PutSyncWithData  ex: {0}, Error reading from WebAPI", ex);
                throw;
            }
            return resultStream;
        }


        // TBD
        public bool ReadNextWithBlob(ITracingService tracingService, Connector connector, out int newVal)
        {
            return ReadNext(tracingService, connector, out newVal);
        }
          
          

        #endregion Web API calls

        public ConnectorPlugin(string config)
        {
            if ( !String.IsNullOrEmpty(config))
            {
               
            }
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            try
            {
                tracingService.Trace("ConnectorPlugin: Config Execute BEG.");
                IPluginExecutionContext context = (Microsoft.Xrm.Sdk.IPluginExecutionContext)serviceProvider.GetService(typeof(Microsoft.Xrm.Sdk.IPluginExecutionContext));
                try
                {
                    if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                    {
                        // Obtain the target entity from the input parameters.
				        Entity entity               = (Entity)context.InputParameters["Target"];
                        
                        // Find associated Connector
                        Connector connector         = Connector.FindConnectors(serviceProvider,context,entity.LogicalName);
                        if ( connector != null )
                        {
                            tracingService.Trace(string.Format("ConnectorPlugin: RequestType: {0}.",connector.RequestType));
                            int newVal              = 0;
                            switch (connector.RequestType)
                            {
                                case RequestType.NoData:
                                    if (ReadNext(tracingService, connector, out newVal))
                                        context.SharedVariables.Add("ConnectorId", newVal.ToString());
                                    else context.SharedVariables.Add("ConnectorId", "Error");
                                    break;
                                case RequestType.DataString:
                                    if (ReadNextWithData(tracingService, connector, entity.Attributes[connector.SourceAttribute].ToString(),out newVal))
                                        context.SharedVariables.Add("ConnectorId", newVal.ToString());
                                    else context.SharedVariables.Add("ConnectorId", "Error");
                                    break;
                                case RequestType.DataBlob:
                                    if (ReadNextWithBlob(tracingService, connector, out newVal))
                                        context.SharedVariables.Add("ConnectorId", newVal.ToString());
                                    else context.SharedVariables.Add("ConnectorId", "Error");
                                    break;
                            }
                        }
                    }
                }

                catch (Exception exception)
                {
                    throw new InvalidPluginExecutionException(String.Format(CultureInfo.InvariantCulture,
                        "ConnectorPlugin: Execute ex {0}", exception.Message), exception);
                }
            }
            catch (Exception e)
            {
                tracingService.Trace("ConnectorPlugin: Execute Ex: {0}", e.Message);
                throw;
            }
        }
    
    }
}

