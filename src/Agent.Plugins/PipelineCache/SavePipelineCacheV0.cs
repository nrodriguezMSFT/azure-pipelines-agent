using Agent.Sdk;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Agent.Plugins.PipelineCache
{    
    // Caller: PublishPipelineArtifact task
    // Can be invoked from a build run or a release run should a build be set as the artifact. 
    public class SavePipelineCacheV0 : PipelineCacheTaskPluginBase
    {
        public override Guid Id => PipelineCachePluginConstants.SaveCacheTaskId;

        protected override async Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context, 
            string targetPath, 
            string fingerPrint,
            CancellationToken token)
        {
            //return Task.CompletedTask;
            string[] fingerPrints = fingerPrint.Split(
                new[] { "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );
            PipelineCacheServer server = new PipelineCacheServer();
            await server.UploadAsync(context,fingerPrints, targetPath,token);
        }
    }
}