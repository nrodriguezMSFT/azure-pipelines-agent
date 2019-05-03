using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private ArtifactClientTelemetry artifactClientTelemetry;
        // Temporary environment variable used to enable telemetry.
        // TODO: Remove TelemetryEnabled when telemetry is stable to enable capturing telemetry by default.
        private readonly bool TelemetryEnabled = int.Parse(Environment.GetEnvironmentVariable("VSO_BUILD_DROP_TELEMETRY", EnvironmentVariableTarget.Process) ?? "0") == 1;

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
            DedupManifestArtifactClient dedupManifestClient = CreateDedupManifestClient(context, connection, pipelineClientType);

            //Upload the pipeline artifact.
            PublishResult result;
            if (TelemetryEnabled)
            {
                ArtifactTelemetryRecord artifactRecord = artifactClientTelemetry.CreateRecord(nameof(UploadAsync));
                try
                {
                    result = await this.artifactClientTelemetry.MeasureActionAsync(
                        record: artifactRecord,
                        actionAsync: async () =>
                        {
                            return await dedupManifestClient.PublishAsync(source, cancellationToken);
                        }
                    ).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    ErrorTelemetryRecord errorRecord = artifactClientTelemetry.CreateError(exception, nameof(UploadAsync), name);
                    this.artifactClientTelemetry.SendErrorTelemetry(errorRecord);
                    throw;
                }
            }
            else
            {
                result = await dedupManifestClient.PublishAsync(source, cancellationToken);
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
            DedupManifestArtifactClient dedupManifestClient = CreateDedupManifestClient(context, connection, pipelineClientType);
            BuildServer buildHelper = new BuildServer(connection);

            // Placeholder telemetry
            if (TelemetryEnabled)
            {
                ArtifactTelemetryRecord downloadAsyncEntry = artifactClientTelemetry.CreateRecord("DownloadAsyncEntry");
                artifactClientTelemetry.SendRecord(downloadAsyncEntry);

                try
                {
                    throw new Exception("test error");
                }
                catch (Exception exception)
                {
                    ErrorTelemetryRecord errorRecord = artifactClientTelemetry.CreateError(exception, nameof(DownloadAsync));
                    this.artifactClientTelemetry.SendErrorTelemetry(errorRecord);
                }
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

                    if (TelemetryEnabled)
                    {
                        ArtifactTelemetryRecord artifactRecord = artifactClientTelemetry.CreateRecord(nameof(DownloadAsync));
                        try
                        {
                            await this.artifactClientTelemetry.MeasureActionAsync(
                                record: artifactRecord,
                                actionAsync: async () =>
                                {
                                    await dedupManifestClient.DownloadAsync(options, cancellationToken);
                                }).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            ErrorTelemetryRecord errorRecord = artifactClientTelemetry.CreateError(exception, nameof(DownloadAsync));
                            this.artifactClientTelemetry.SendErrorTelemetry(errorRecord);
                        }
                    }
                    else
                    {
                        await dedupManifestClient.DownloadAsync(options, cancellationToken);
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
                
                if (TelemetryEnabled)
                {
                    ArtifactTelemetryRecord artifactRecord = artifactClientTelemetry.CreateRecord(nameof(DownloadAsync));
                    await this.artifactClientTelemetry.MeasureActionAsync(
                        record: artifactRecord,
                        actionAsync: async () =>
                        {
                            await dedupManifestClient.DownloadAsync(options, cancellationToken);
                        });
                }
                else
                {
                    await dedupManifestClient.DownloadAsync(options, cancellationToken);
                }
            }
            else
            {
                throw new InvalidOperationException("Unreachable code!");
            }
        }

        /// <summary>
        /// Creates a DedupManifestArtifactClient and exposes ArtifactClientTelemetry.
        /// </summary>
        private DedupManifestArtifactClient CreateDedupManifestClient(AgentTaskPluginExecutionContext context, VssConnection connection, ClientType clientType)
        {
            var tracer = new CallbackAppTraceSource(str => context.Output(str), SourceLevels.Information);
            artifactClientTelemetry = new ArtifactClientTelemetry(tracer, clientType);
            return DedupManifestArtifactClientFactory.CreateDedupManifestClient(connection, artifactClientTelemetry, tracer);
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