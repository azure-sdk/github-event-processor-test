using System;
using System.IO;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using System.Text.Json;
using Octokit.Internal;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // JRS - Fix this so "dotnet run" aren't the first two args
            Console.WriteLine($"args.Length={args.Length}");
            if (args.Length < 2)
            {
                Console.WriteLine("Error: There are two required arguments:");
                Console.WriteLine(" 1. The github.event_name");
                Console.WriteLine(" 2. The GITHUB_PAYLOAD json file.");
                Environment.Exit(1);
		        return;
            }
            if (!File.Exists(args[1]))
            {
                Console.WriteLine($"Error: The GITHUB_PAYLOAD file {args[1]} does not exist.");
                Environment.Exit(1);
		        return;
            }

            string eventName = args[1];
            var serializer = new SimpleJsonSerializer();
            string rawJson = File.ReadAllText(args[1]);
            GitHubEventClient gitHubEventClient = new GitHubEventClient(OrgConstants.ProductHeaderName);
            await gitHubEventClient.WriteRateLimits("RateLimit at start of execution:");
            switch (eventName)
            {
                case EventConstants.issues:
                    {
                        IssueEventGitHubPayload issueEventPayload = serializer.Deserialize<IssueEventGitHubPayload>(rawJson);
                        await IssueProcessing.ProcessIssueEvent(gitHubEventClient, issueEventPayload);
                        break;
                    }
                case EventConstants.issue_comment:
                    {
                        IssueCommentPayload issueCommentPayload = serializer.Deserialize<IssueCommentPayload>(rawJson);
                        // IssueComment events are for both issues and pull requests. If the comment is on a pull request,
                        // then Issue's PullRequest object in the payload will be non-null
                        if (issueCommentPayload.Issue.PullRequest != null)
                        {
                            await PullRequestCommentProcessing.ProcessPullRequestCommentEvent(gitHubEventClient, issueCommentPayload);
                        }
                        else
                        {
                            await IssueCommentProcessing.ProcessIssueCommentEvent(gitHubEventClient, issueCommentPayload);
                        }

                        break;
                    }
                case EventConstants.pull_request_target:
                    {
                        // The pull_request, because of the auto_merge processing, requires more than just deserialization of the
                        // the rawJson.
                        PullRequestEventGitHubPayload prEventPayload = PullRequestProcessing.DeserializePullRequest(rawJson, serializer);
                        await PullRequestProcessing.ProcessPullRequestEvent(gitHubEventClient, prEventPayload);
                        break;
                    }
                case EventConstants.pull_request_review:
                    {
                        PullRequestReviewEventPayload prReviewEventPayload = serializer.Deserialize<PullRequestReviewEventPayload>(rawJson);
                        await PullRequestReviewProcessing.ProcessPullRequestReviewEvent(gitHubEventClient, prReviewEventPayload);
                        break;
                    }
                case EventConstants.schedule:
                    {
                        ScheduledEventGitHubPayload scheduledEventPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
                        await ScheduledEventProcessing.ProcessScheduledEvent(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                default:
                    {
                        Console.WriteLine($"Event type {eventName} does not have any processing associated with it.");
                        break;
                    }
            }
            await gitHubEventClient.WriteRateLimits("RateLimit at end of execution:");
        }
    }
}
