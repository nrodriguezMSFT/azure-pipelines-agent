using System;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;

namespace Agent.Plugins.PipelineArtifact.Telemetry
{
    /// <summary>
    /// Generic telemetry record for use with Pipeline events.
    /// </summary>
    public abstract class PipelineTelemetryRecord : BlobStoreTelemetryRecord
    {
        public Guid PlanId { get; private set; }
        public Guid JobId { get; private set; }
        public Guid TaskInstanceId { get; private set; }

        public PipelineTelemetryRecord(TelemetryInformationLevel level, Uri baseAddress, string eventNamePrefix, string eventNameSuffix, AgentTaskPluginExecutionContext context, uint attemptNumber = 1)
            : base(level, baseAddress, eventNamePrefix, eventNameSuffix, attemptNumber)
        {
            PlanId = Guid.Parse(context.Variables["system.planId"].Value);
            JobId = Guid.Parse(context.Variables["system.jobId"].Value);
            TaskInstanceId = Guid.Parse(context.Variables["system.taskInstanceId"].Value);
        }
    }
}