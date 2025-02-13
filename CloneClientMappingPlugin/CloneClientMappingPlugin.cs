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
            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));



            // Obtain the organization service reference.
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));



            // Ensure the plugin is triggered on Create operation
            if (context.MessageName.ToLower() != "create")
            {
                return;
            }



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



                    // Fetch verified employee record
                    string fetchXml = $@"
                    <fetch top='1'>
                        <entity name='infcf_verifiedemployee'>
                            <attribute name='infcf_empcode' />



                           <filter type='and'>



                                <condition attribute='infcf_verifiedemployeeid' operator='eq' value='{verifiedemployeeID}' />
<condition attribute='infcf_employeemaster' operator='null' />
                            </filter>
                        </entity>
                    </fetch>";



                    EntityCollection Dvverifiedemployee = service.RetrieveMultiple(new FetchExpression(fetchXml));



                    if (Dvverifiedemployee.Entities.Count == 0)
                    {
                        throw new InvalidPluginExecutionException($"No Record found with ID {verifiedemployeeID}");
                    }



                    // Retrieve the employee code from the record
                    Entity verifiedEmployeeEntity = Dvverifiedemployee.Entities[0];
                    VerifiedEmployees verifiedEmployee = new VerifiedEmployees
                    {
                        EmpCode = verifiedEmployeeEntity.GetAttributeValue<string>("infcf_empcode")
                    };





                    // Fetch Employee Master record based on Employee Code
                    string fetchXml2 = $@"
                    <fetch top='1'>
                        <entity name='infcf_employeemaster'>
                            <attribute name='infcf_employeemasterid' />
                             <attribute name='infcf_employeeidentification' />
                            <filter>
                                <condition attribute='infcf_employeecode' operator='eq' value='{verifiedEmployee.EmpCode}' />
                            </filter>
                            <order attribute='infcf_employeeidentification' descending='true' />
                        </entity>
                    </fetch>";



                    EntityCollection Dvemployeemaster = service.RetrieveMultiple(new FetchExpression(fetchXml2));



                    if (Dvemployeemaster.Entities.Count == 0)
                    {
                        throw new InvalidPluginExecutionException($"No Employee Master record found for Employee Code: {verifiedEmployee.EmpCode}");
                    }



                    // Get the first record from the result
                    Entity employeemasterEntity = Dvemployeemaster.Entities[0];
                    // Retrieve the Employee Master ID correctly as GUID
                    Guid employeeMasterId = employeemasterEntity.GetAttributeValue<Guid>("infcf_employeemasterid");



                    DvEmployeeMaster employeeMaster = new DvEmployeeMaster
                    {
                        EmployeeMaster = employeemasterEntity.GetAttributeValue<Guid>("infcf_employeemasterid").ToString(),
                        EmployeeIdentification = employeemasterEntity.GetAttributeValue<string>("infcf_employeeidentification")
                    };



                    // Correctly setting the EntityReference
                    targetEntity["infcf_employeemaster"] = new EntityReference("infcf_employeemaster", employeeMasterId);





                    service.Update(targetEntity);



                    tracingService.Trace($"Successfully updated Verified Employee {verifiedemployeeID} with Employee Master {employeeMaster.EmployeeMaster}.");
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Exception: {ex.Message} {ex.StackTrace}");
                throw new InvalidPluginExecutionException("An error occurred in the plugin.", ex);
            }
        }
    }
}
