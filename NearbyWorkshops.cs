using System;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class NearbyWorkshops : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        var service = serviceFactory.CreateOrganizationService(context.UserId);

        if (context.MessageName != "Create" && context.MessageName != "Update")
            return;

        Entity workshop = null;

        if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            workshop = (Entity)context.InputParameters["Target"];

        if (workshop == null || workshop.LogicalName != "edu_workshop" || context.Depth > 1)
            return;

        DateTime? workshopDate = null;

        if (workshop.Contains("edu_date"))
            workshopDate = (DateTime)workshop["edu_date"];
        else if (context.MessageName == "Update")
        {
            var dbWorkshop = service.Retrieve("edu_workshop", workshop.Id, new ColumnSet("edu_date"));
            if (dbWorkshop.Contains("edu_date"))
                workshopDate = (DateTime)dbWorkshop["edu_date"];
        }

        if (workshopDate == null)
            return;

        DateTime startDate = workshopDate.Value.AddDays(-7);
        DateTime endDate = workshopDate.Value.AddDays(7);

        var query = new QueryExpression("edu_workshop")
        {
            ColumnSet = new ColumnSet("edu_name", "edu_date"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("edu_date", ConditionOperator.OnOrAfter, startDate),
                    new ConditionExpression("edu_date", ConditionOperator.OnOrBefore, endDate),
                    new ConditionExpression("edu_workshopid", ConditionOperator.NotEqual, workshop.Id)
                }
            }
        };

        var nearbyWorkshops = service.RetrieveMultiple(query);

        string nearbyNames = string.Empty;

        if (nearbyWorkshops.Entities.Count > 0)
        {
            var names = nearbyWorkshops.Entities
                .Select(w => w.Contains("edu_name") ? w["edu_name"].ToString() : string.Empty)
                .Where(n => !string.IsNullOrEmpty(n));

            nearbyNames = string.Join(", ", names);
        }

        var updateWorkshop = new Entity("edu_workshop", workshop.Id);
        updateWorkshop["edu_nearbyworkshops"] = nearbyNames;
        service.Update(updateWorkshop);
    }
}