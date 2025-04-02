using CloneClientMappingPlugin.DataverseObjects;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace CloneClientMappingPlugin
{
    public class CreatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain required services
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Ensure the plugin is triggered on Create operation
            if (context.MessageName.ToLower() != "create") return;

            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity targetEntity))
            {
                tracingService.Trace("InputParameters does not contain a valid 'Target' entity. Exiting plugin execution.");
                return;
            }

            tracingService.Trace($"Target Entity Logical Name: {targetEntity.LogicalName}");

            try
            {
                if (targetEntity.LogicalName != "infcf_verifiedemployee") return;

                Guid verifiedEmployeeID = targetEntity.Id;
                tracingService.Trace($"Verified Employee ID: {verifiedEmployeeID}");

                // Fetch verified employee where 'infcf_employeemaster' is NULL
                string fetchXmlVerifiedEmployee = $@"
                <fetch top='1'>
                    <entity name='infcf_verifiedemployee'>
                        <attribute name='infcf_empcode' />
                        <filter type='and'>
                            <condition attribute='infcf_verifiedemployeeid' operator='eq' value='{verifiedEmployeeID}' />
                            <condition attribute='infcf_employeemaster' operator='null' />
                        </filter>
                    </entity>
                </fetch>";

                EntityCollection verifiedEmployeeRecords = service.RetrieveMultiple(new FetchExpression(fetchXmlVerifiedEmployee));

                if (verifiedEmployeeRecords.Entities.Count == 0)
                {
                    tracingService.Trace($"No records found with ID {verifiedEmployeeID} where infcf_employeemaster is NULL.");
                    return; // Exit safely instead of throwing an error
                }

                // Retrieve employee code
                Entity verifiedEmployeeEntity = verifiedEmployeeRecords.Entities[0];
                string employeeCode = verifiedEmployeeEntity.GetAttributeValue<string>("infcf_empcode");

                if (string.IsNullOrEmpty(employeeCode))
                {
                    tracingService.Trace($"Employee Code is NULL for Verified Employee ID: {verifiedEmployeeID}. Skipping update.");
                    return;
                }

                tracingService.Trace($"Found Employee Code: {employeeCode}");

                // Fetch Employee Master record based on Employee Code
                string fetchXmlEmployeeMaster = $@"
                <fetch top='1'>
                    <entity name='infcf_employeemaster'>
                        <attribute name='infcf_employeemasterid' />
                        <attribute name='infcf_employeeidentification' />
                        <filter>
                            <condition attribute='infcf_employeecode' operator='eq' value='{employeeCode}' />
                        </filter>
                        <order attribute='infcf_employeeidentification' descending='true' />
                    </entity>
                </fetch>";

                EntityCollection employeeMasterRecords = service.RetrieveMultiple(new FetchExpression(fetchXmlEmployeeMaster));

                if (employeeMasterRecords.Entities.Count == 0)
                {
                    tracingService.Trace($"No Employee Master record found for Employee Code: {employeeCode}");
                    return; // Exit safely
                }

                // Retrieve Employee Master ID
                Entity employeeMasterEntity = employeeMasterRecords.Entities[0];
                Guid employeeMasterId = employeeMasterEntity.GetAttributeValue<Guid>("infcf_employeemasterid");

                // Set Employee Master reference
                targetEntity["infcf_employeemaster"] = new EntityReference("infcf_employeemaster", employeeMasterId);

                // Update Verified Employee record
                service.Update(targetEntity);

                tracingService.Trace($"Successfully updated Verified Employee {verifiedEmployeeID} with Employee Master {employeeMasterId}.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Exception: {ex.Message} \n {ex.StackTrace}");
                throw new InvalidPluginExecutionException("An error occurred in the plugin.", ex);
            }
        }
    }
}
