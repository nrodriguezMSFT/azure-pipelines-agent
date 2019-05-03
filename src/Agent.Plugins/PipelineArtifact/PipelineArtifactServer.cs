using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.Content.Common.Telemetry;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.WebApi.Telemetry;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Newtonsoft.Json;
using Agent.Sdk;

namespace Agent.Plugins.PipelineArtifact
{    
    // A wrapper of DedupManifestArtifactClient, providing basic functionalities such as uploading and downloading pipeline artifacts.
    public class PipelineArtifactServer
    {
        public static readonly string RootId = "RootId";
        public static readonly string ProofNodes = "ProofNodes";
        private const ClientType pipelineClientType = ClientType.PipelineArtifact;

        // Upload from target path to Azure DevOps BlobStore service through DedupManifestArtifactClient, then associate it with the build
        internal async Task UploadAsync(
            AgentTaskPluginExecutionContext context,
            Guid projectId,
            int pipelineId,
            string name,
            string source,
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection, pipelineClientType);

            //Upload the pipeline artifact.
            PublishResult result;
            BlobStoreTelemetryRecord blobStoreRecord = dedupManifestClient.Telemetry.CreateRecord(nameof(UploadAsync));
            try
            {
                result = await dedupManifestClient.Telemetry.MeasureActionAsync(
                    record: blobStoreRecord,
                    actionAsync: async () =>
                    {
                        return await dedupManifestClient.PublishAsync(source, cancellationToken);
                    }
                ).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                ErrorTelemetryRecord errorRecord = dedupManifestClient.Telemetry.CreateError(exception, nameof(UploadAsync), name);
                dedupManifestClient.Telemetry.SendErrorTelemetry(errorRecord);
                throw;
            }

            // 2) associate the pipeline artifact with an build artifact
            BuildServer buildHelper = new BuildServer(connection);
            Dictionary<string, string> propertiesDictionary = new Dictionary<string, string>();
            propertiesDictionary.Add(RootId, result.RootId.ValueString);
            propertiesDictionary.Add(ProofNodes, StringUtil.ConvertToJson(result.ProofNodes.ToArray()));
            var artifact = await buildHelper.AssociateArtifact(projectId, pipelineId, name, ArtifactResourceTypes.PipelineArtifact, result.ManifestId.ValueString, propertiesDictionary, cancellationToken);
            context.Output(StringUtil.Loc("AssociateArtifactWithBuild", artifact.Id, pipelineId));
        }

        // Download pipeline artifact from Azure DevOps BlobStore service through DedupManifestArtifactClient to a target path
        // Old V0 function
        internal Task DownloadAsync(
            AgentTaskPluginExecutionContext context,
            Guid projectId,
            int pipelineId,
            string artifactName,
            string targetDir,
            CancellationToken cancellationToken)
        {
            var downloadParameters = new PipelineArtifactDownloadParameters
            {
                ProjectRetrievalOptions = BuildArtifactRetrievalOptions.RetrieveByProjectId,
                ProjectId = projectId,
                PipelineId = pipelineId,
                ArtifactName = artifactName,
                TargetDirectory = targetDir
            };

            return this.DownloadAsync(context, downloadParameters, DownloadOptions.SingleDownload, cancellationToken);
        }

        // Download with minimatch patterns.
        internal async Task DownloadAsync(
            AgentTaskPluginExecutionContext context,
            PipelineArtifactDownloadParameters downloadParameters,
            DownloadOptions downloadOptions, 
            CancellationToken cancellationToken)
        {
            VssConnection connection = context.VssConnection;
            DedupManifestArtifactClient dedupManifestClient = DedupManifestArtifactClientFactory.CreateDedupManifestClient(context, connection, pipelineClientType);
            BuildServer buildHelper = new BuildServer(connection);

            // Placeholder telemetry
            BlobStoreTelemetryRecord downloadAsyncEntry = dedupManifestClient.Telemetry.CreateRecord("DownloadAsyncEntry");
            dedupManifestClient.Telemetry.SendRecord(downloadAsyncEntry);
            try
            {
                throw new Exception("test error");
            }
            catch (Exception exception)
            {
                ErrorTelemetryRecord errorRecord = dedupManifestClient.Telemetry.CreateError(exception, nameof(DownloadAsync));
                dedupManifestClient.Telemetry.SendErrorTelemetry(errorRecord);
            }

            // download all pipeline artifacts if artifact name is missing
            if (downloadOptions == DownloadOptions.MultiDownload)
            {
                List<BuildArtifact> artifacts;
                if (downloadParameters.ProjectRetrievalOptions == BuildArtifactRetrievalOptions.RetrieveByProjectId)
                {
                    artifacts = await buildHelper.GetArtifactsAsync(downloadParameters.ProjectId, downloadParameters.PipelineId, cancellationToken);
                }
                else if (downloadParameters.ProjectRetrievalOptions == BuildArtifactRetrievalOptions.RetrieveByProjectName)
                {
                    if (string.IsNullOrEmpty(downloadParameters.ProjectName))
                    {
                        throw new InvalidOperationException("Project name can't be empty when trying to fetch build artifacts!");
                    }
                    else
                    {
                        artifacts = await buildHelper.GetArtifactsWithProjectNameAsync(downloadParameters.ProjectName, downloadParameters.PipelineId, cancellationToken);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unreachable code!");
                }

                IEnumerable<BuildArtifact> pipelineArtifacts = artifacts.Where(a => a.Resource.Type == pipelineClientType.ToString());
                if (pipelineArtifacts.Count() == 0)
                {
                    throw new ArgumentException("Could not find any pipeline artifacts in the build.");
                }
                else
                {
                    context.Output(StringUtil.Loc("DownloadingMultiplePipelineArtifacts", pipelineArtifacts.Count()));

                    var artifactNameAndManifestIds = pipelineArtifacts.ToDictionary(
                        keySelector: (a) => a.Name, // keys should be unique, if not something is really wrong
                        elementSelector: (a) => DedupIdentifier.Create(a.Resource.Data));
                    // 2) download to the target path
                    var options = DownloadPipelineArtifactOptions.CreateWithMultiManifestIds(
                        artifactNameAndManifestIds,
                        downloadParameters.TargetDirectory,
                        proxyUri: null,
                        minimatchPatterns: downloadParameters.MinimatchFilters);

                    BlobStoreTelemetryRecord blobStoreRecord = dedupManifestClient.Telemetry.CreateRecord(nameof(DownloadAsync));
                    try
                    {
                        await dedupManifestClient.Telemetry.MeasureActionAsync(
                            record: blobStoreRecord,
                            actionAsync: async () =>
                            {
                                await dedupManifestClient.DownloadAsync(options, cancellationToken);
                            }).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        ErrorTelemetryRecord errorRecord = dedupManifestClient.Telemetry.CreateError(exception, nameof(DownloadAsync));
                        dedupManifestClient.Telemetry.SendErrorTelemetry(errorRecord);
                    }
                }
            }
            else if (downloadOptions == DownloadOptions.SingleDownload)
            {
                // 1) get manifest id from artifact data
                BuildArtifact buildArtifact;
                if (downloadParameters.ProjectRetrievalOptions == BuildArtifactRetrievalOptions.RetrieveByProjectId)
                {
                    buildArtifact = await buildHelper.GetArtifact(downloadParameters.ProjectId, downloadParameters.PipelineId, downloadParameters.ArtifactName, cancellationToken);
                }
                else if (downloadParameters.ProjectRetrievalOptions == BuildArtifactRetrievalOptions.RetrieveByProjectName)
                {
                    if (string.IsNullOrEmpty(downloadParameters.ProjectName))
                    {
                        throw new InvalidOperationException("Project name can't be empty when trying to fetch build artifacts!");
                    }
                    else
                    {
                        buildArtifact = await buildHelper.GetArtifactWithProjectNameAsync(downloadParameters.ProjectName, downloadParameters.PipelineId, downloadParameters.ArtifactName, cancellationToken);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unreachable code!");
                }

                var manifestId = DedupIdentifier.Create(buildArtifact.Resource.Data);
                var options = DownloadPipelineArtifactOptions.CreateWithManifestId(
                    manifestId,
                    downloadParameters.TargetDirectory,
                    proxyUri: null,
                    minimatchPatterns: downloadParameters.MinimatchFilters);
                
                BlobStoreTelemetryRecord blobStoreRecord = dedupManifestClient.Telemetry.CreateRecord(nameof(DownloadAsync));
                await dedupManifestClient.Telemetry.MeasureActionAsync(
                    record: blobStoreRecord,
                    actionAsync: async () =>
                    {
                        await dedupManifestClient.DownloadAsync(options, cancellationToken);
                    });
            }
            else
            {
                throw new InvalidOperationException("Unreachable code!");
            }
        }
    }

    internal class PipelineArtifactDownloadParameters
    {
        /// <remarks>
        /// Options on how to retrieve the build using the following parameters.
        /// </remarks>
        public BuildArtifactRetrievalOptions ProjectRetrievalOptions { get; set; }
        /// <remarks>
        /// Either project ID or project name need to be supplied.
        /// </remarks>
        public Guid ProjectId { get; set; }
        /// <remarks>
        /// Either project ID or project name need to be supplied.
        /// </remarks>
        public string ProjectName { get; set; }
        public int PipelineId { get; set; }
        public string ArtifactName { get; set; }
        public string TargetDirectory { get; set; }
        public string[] MinimatchFilters { get; set; }
    }

    internal enum BuildArtifactRetrievalOptions
    {
        RetrieveByProjectId,
        RetrieveByProjectName
    }

    internal enum DownloadOptions
    {
        SingleDownload,
        MultiDownload
    }
}