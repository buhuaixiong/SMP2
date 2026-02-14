using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private async Task<Dictionary<string, object?>?> GetRfqWithLineItemsAsync(
        long rfqId,
        CancellationToken cancellationToken)
    {
        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);

        if (rfq == null)
        {
            return null;
        }

        var rfqSnake = NodeCaseMapper.ToSnakeCaseDictionary(rfq);
        rfqSnake["required_documents"] = ParseJsonValue(rfq.RequiredDocuments, new List<object>());
        rfqSnake["evaluation_criteria"] = ParseJsonValue(rfq.EvaluationCriteria, new Dictionary<string, object?>());

        if (rfq.IsLineItemMode)
        {
            var lineItems = await _dbContext.RfqLineItems.AsNoTracking()
                .Where(li => li.RfqId == rfqId)
                .OrderBy(li => li.LineNumber)
                .ToListAsync(cancellationToken);

            var attachmentGroups = await _dbContext.RfqAttachments.AsNoTracking()
                .Where(att => att.LineItemId != null && lineItems.Select(li => li.Id).Contains(att.LineItemId.Value))
                .GroupBy(att => att.LineItemId!.Value)
                .ToDictionaryAsync(g => (long)g.Key, g => g.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList(), cancellationToken);

            var lineItemDicts = new List<Dictionary<string, object?>>();
            foreach (var lineItem in lineItems)
            {
                var lineItemSnake = NodeCaseMapper.ToSnakeCaseDictionary(lineItem);
                if (attachmentGroups.TryGetValue(lineItem.Id, out var attachments))
                {
                    lineItemSnake["attachments"] = attachments;
                }
                else
                {
                    lineItemSnake["attachments"] = new List<Dictionary<string, object?>>();
                }
                lineItemDicts.Add(lineItemSnake);
            }

            rfqSnake["line_items"] = lineItemDicts;
        }

        return (Dictionary<string, object?>)CaseTransform.ToCamelCase(rfqSnake)!;
    }
}
