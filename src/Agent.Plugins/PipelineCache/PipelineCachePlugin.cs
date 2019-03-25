using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Agent.Sdk;

namespace Agent.Plugins.PipelineCache
{
    public abstract class PipelineCacheTaskPluginBase : IAgentTaskPlugin
    {
        public abstract Guid Id { get; }
        public string Version => "0.1.0"; // Publish and Download tasks will be always on the same version.
        public string Stage => "main";

        public async Task RunAsync(AgentTaskPluginExecutionContext context, CancellationToken token)
        {
            ArgUtil.NotNull(context, nameof(context));

            string fingerprint = context.GetInput(PipelineCacheTaskPluginConstants.Fingerprints, required: true);

            // TODO: Translate targetPath from container to host (Ting)
            string targetPath = context.GetInput(PipelineCacheTaskPluginConstants.TargetPath, required: true);

            string salt = context.GetInput(PipelineCacheTaskPluginConstants.Salt, required: true);            

            await ProcessCommandInternalAsync(
                context, 
                targetPath, 
                fingerprint, 
                salt,
                token);
        }

        // Process the command with preprocessed arguments.
        protected abstract Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context, 
            string targetPath, 
            string fingerprint,
            string salt,
            CancellationToken token);

            
        // Properties set by tasks
        protected static class PipelineCacheTaskPluginConstants
        {
            public static readonly string Fingerprints = "fingerprints"; // this needs to match the input in the task.
            public static readonly string TargetPath = "targetPath";
            public static readonly string PipelineId = "pipelineId";
            public static readonly string VariableToSetOnCacheHit = "cacheHitVar";
            public static readonly string Salt = "salt";
            
        }
    }
}