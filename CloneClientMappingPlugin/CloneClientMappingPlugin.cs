using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;



namespace CloneClientMappingPlugin
{

    public class CreatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the organization service reference which you can use to create or update records.
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            // Ensure that the plugin is triggered on the Create operation
            if (context.MessageName.ToLower() != "create")
            {
                return;
            }

            // Obtain the target entity from the input parameters (it should be the entity being created)
            
            // Validate the target entity
            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity))
            {
                tracingService.Trace("InputParameters does not contain a valid 'Target' entity. Exiting plugin execution.");
                return;
            }
            try
            {
                
                if (targetEntity.LogicalName == "infcf_verifiedemployee") 
                {
                    Guid verifiedemployeeID = targetEntity.Id;

                    string fetchXml = $@"<fetch top='1'>
                  <entity name='sam_workshop'>
                  <attribute name=""sam_details"" />
                  <attribute name=""sam_sqlworkshopid"" />
                   <filter>
                       <condition attribute='infcf_verifiedemployeeid' operator='eq' value='{verifiedemployeeID}' />
                   </filter>
                  </entity>
                </fetch>";

                    service.Update(targetEntity);
                }
            }
            catch (Exception ex)
            {
                
                throw new InvalidPluginExecutionException("An error occurred in the plugin.", ex);
            }
        }
    }

}
