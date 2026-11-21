using DispatchApi.Dtos;
using DispatchApi.Models;

namespace DispatchApi.Services;

/// <summary>
/// Computes the live SLA state of a job.
/// Response stage: technician must arrive (ActualStart) by ResponseDueAt.
/// Resolution stage: job must complete (ActualEnd) by ResolutionDueAt.
/// A job is AtRisk when its projected time is after the due time, when it is unassigned close to the deadline,
/// when it is on hold, or when the remaining time is within the policy's AtRiskThresholdMinutes.
/// </summary>
public static class SlaCalculator
{
    public static SlaStatusDto Compute(JobStatus status, DateTime? scheduledStart, int estimatedMinutes, DateTime responseDue,
        DateTime resolutionDue, DateTime? actualStart, DateTime? actualEnd, int thresholdMinutes, DateTime now)
    {
        if (status == JobStatus.Cancelled)
            return new(SlaState.NotApplicable, SlaStage.Closed, null, null, null, "Cancelled", false, null);

        if (status == JobStatus.Completed)
        {
            var respMet = actualStart.HasValue && actualStart <= responseDue;
            var resMet = actualEnd.HasValue && actualEnd <= resolutionDue;
            return new(respMet && resMet ? SlaState.Met : SlaState.Breached, SlaStage.Closed, resolutionDue, null, actualEnd,
                respMet && resMet ? null : !respMet ? "Response SLA missed" : "Resolution SLA missed", respMet, resMet);
        }

        if (actualStart is null)
        {
            var remaining = (int)Math.Floor((responseDue - now).TotalMinutes);
            var projected = scheduledStart;
            if (remaining < 0)
                return new(SlaState.Breached, SlaStage.Response, responseDue, remaining, projected, "Response overdue", false, null);
            if (status == JobStatus.OnHold)
                return new(SlaState.AtRisk, SlaStage.Response, responseDue, remaining, projected, "Job on hold", true, null);
            if (projected is null)
            {
                var risky = remaining <= thresholdMinutes * 2;
                return new(risky ? SlaState.AtRisk : SlaState.OnTrack, SlaStage.Response, responseDue, remaining, null,
                    risky ? "Unassigned close to response deadline" : "Unassigned", true, null);
            }
            if (projected > responseDue)
                return new(SlaState.AtRisk, SlaStage.Response, responseDue, remaining, projected, "Scheduled after response due time", true, null);
            if (projected < now && status is JobStatus.Assigned or JobStatus.Dispatched)
                return new(SlaState.AtRisk, SlaStage.Response, responseDue, remaining, projected, "Technician running late", true, null);
            if (remaining <= thresholdMinutes && status is JobStatus.Assigned)
                return new(SlaState.AtRisk, SlaStage.Response, responseDue, remaining, projected, "Not yet dispatched", true, null);
            return new(SlaState.OnTrack, SlaStage.Response, responseDue, remaining, projected, null, true, null);
        }
        else
        {
            var responseMet = actualStart <= responseDue;
            var remaining = (int)Math.Floor((resolutionDue - now).TotalMinutes);
            var projectedEnd = actualStart.Value.AddMinutes(estimatedMinutes);
            if (projectedEnd < now) projectedEnd = now.AddMinutes(15); // overrunning: assume at least 15 more minutes
            if (remaining < 0)
                return new(SlaState.Breached, SlaStage.Resolution, resolutionDue, remaining, projectedEnd, "Resolution overdue", responseMet, false);
            if (!responseMet)
                return new(SlaState.Breached, SlaStage.Resolution, resolutionDue, remaining, projectedEnd, "Response SLA missed", false, null);
            if (status == JobStatus.OnHold)
                return new(SlaState.AtRisk, SlaStage.Resolution, resolutionDue, remaining, null, "Job on hold", true, null);
            if (projectedEnd > resolutionDue)
                return new(SlaState.AtRisk, SlaStage.Resolution, resolutionDue, remaining, projectedEnd, "Projected completion after due time", true, null);
            if (remaining <= thresholdMinutes)
                return new(SlaState.AtRisk, SlaStage.Resolution, resolutionDue, remaining, projectedEnd, "Resolution deadline approaching", true, null);
            return new(SlaState.OnTrack, SlaStage.Resolution, resolutionDue, remaining, projectedEnd, null, true, null);
        }
    }
}
