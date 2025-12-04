using System;
using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class ParticipantLimitValidation : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        var service = serviceFactory.CreateOrganizationService(context.UserId);

        if (context.MessageName != "Associate" && context.MessageName != "Disassociate")
            return;

        if (context.InputParameters.Contains("Relationship"))
        {
            var relationship = (Relationship)context.InputParameters["Relationship"];
            if (relationship.SchemaName != "edu_edu_workshop_contact")
                return;
        }

        var targetEntityRef = (EntityReference)context.InputParameters["Target"];
        Guid workshopId = targetEntityRef.Id;

        var workshop = service.Retrieve("edu_workshop", workshopId,
            new ColumnSet("edu_maxparticipants", "edu_currentparticipants"));

        int maxParticipants = workshop.Contains("edu_maxparticipants") ? (int)workshop["edu_maxparticipants"] : 0;

        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet(false),
            LinkEntities =
            {
                new LinkEntity("contact", "edu_edu_workshop_contact", "contactid", "contactid", JoinOperator.Inner)
                {
                    LinkCriteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("edu_workshopid", ConditionOperator.Equal, workshopId)
                        }
                    }
                }
            }
        };

        var participants = service.RetrieveMultiple(query);
        int currentCount = participants.Entities.Count;

        if (currentCount > maxParticipants)
        {
            throw new InvalidPluginExecutionException("Cannot add participant. Max limit reached.");
        }

        var updateWorkshop = new Entity("edu_workshop", workshopId);
        updateWorkshop["edu_currentparticipants"] = currentCount;
        service.Update(updateWorkshop);
    }
}
