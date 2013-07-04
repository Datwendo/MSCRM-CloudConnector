using System;
using Microsoft.Xrm.Sdk;

namespace Datwendo.Crm.Sdk
{
	public class InsertConnectorPlugin: IPlugin
	{
        public void Execute(IServiceProvider serviceProvider)
		{
            Microsoft.Xrm.Sdk.IPluginExecutionContext context = (Microsoft.Xrm.Sdk.IPluginExecutionContext)serviceProvider.GetService(typeof(Microsoft.Xrm.Sdk.IPluginExecutionContext));

			if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
				Entity entity = (Entity)context.InputParameters["Target"];
                // Find associated Connector
                Connector connector = Connector.FindConnectors(serviceProvider, context, entity.LogicalName);
                if (connector != null)
                {
                    // Obtain the contact from the execution context shared variables.
                    if (context.SharedVariables.Contains("ConnectorId"))
                    {
                        string result = (string)context.SharedVariables["ConnectorId"];
                        int val = 0;
                        if (!string.IsNullOrEmpty(result))
                        {
                            if (int.TryParse(result, out val))
                                entity.Attributes.Add(connector.AssociatedAttribute, result);
                        }
                    }
				}
			}
		}
	}
}
