using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class ScheduledEventProcessing
    {
        public static async Task ProcessScheduledEvent(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            await CloseStaleIssues(gitHubEventClient, scheduledEventPayload);
            await CloseStalePullRequests(gitHubEventClient, scheduledEventPayload);
            await IdentifyStalePullRequests(gitHubEventClient, scheduledEventPayload);
            await IdentifyStaleIssues(gitHubEventClient, scheduledEventPayload);
            await CloseAddressedIssues(gitHubEventClient, scheduledEventPayload);
            await LockClosedIssues(gitHubEventClient, scheduledEventPayload);
            // The second argument is IssueOrPullRequestNumber which isn't applicable to scheduled events (cron tasks)
            // since they're not going to be changing a single IssueUpdate like rules processing does.
            int numUpdates = await gitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id, 0);

        }

        /// <summary>
        /// Trigger: Daily 1am
        /// Query Criteria
        ///     Issue is open
        ///     Issue has "needs-author-feedback" label
        ///     Issue has "no-recent-activity" label
        ///     Issue was last modified more than 14 days ago
        /// Resulting Action: 
        ///     Close the issue
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task CloseStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseStaleIssues))
            {
                List<string> includeLabels = new List<string>
                {
                    LabelConstants.NeedsAuthorFeedback,
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 14, // more than 14 days old
                                                                 null,
                                                                 includeLabels);
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                foreach (var issue in result.Items)
                {

                }
            }
        }

        /// <summary>
        /// Trigger: weekly, Friday at 5am
        /// Query Criteria
        ///     Pull request is open
        ///     Pull request does NOT have "no-recent-activity" label
        ///     Pull request was last updated more than 60 days ago
        /// Resulting Action: 
        ///     Add "no-recent-activity" label
        ///     Create a comment "Hi @${issueAuthor}.  Thank you for your interest in helping to improve the Azure SDK experience and for your contribution.  We've noticed that there hasn't been recent engagement on this pull request.  If this is still an active work stream, please let us know by pushing some changes or leaving a comment.  Otherwise, we'll close this out in 7 days."
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task IdentifyStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IdentifyStaleIssues))
            {

                List<string> excludeLabels = new List<string>
                {
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 60, // more than 14 days old
                                                                 null,
                                                                 null,
                                                                 excludeLabels);
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

                Console.WriteLine(result.TotalCount);
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is open
        ///     Issue has "needs-author-feedback" label
        ///     Issue does NOT have "no-recent-activity" label
        ///     Issue was last updated more than 7 days ago
        /// Resulting Action: 
        ///     Add "no-recent-activity" label
        ///     Create a comment: "Hi, we're sending this friendly reminder because we haven't heard back from you in **7 days**. We need more information about this issue to help address it. Please be sure to give us your input. If we don't hear back from you within **14 days** of this comment the issue will be automatically closed. Thank you!"
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task IdentifyStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IdentifyStaleIssues))
            {

                List<string> includeLabels = new List<string>
                {
                    LabelConstants.NeedsAuthorFeedback
                };
                List<string> excludeLabels = new List<string>
                {
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 14, // more than 14 days old
                                                                 null,
                                                                 includeLabels,
                                                                 excludeLabels);
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

                Console.WriteLine(result.TotalCount);
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Pull request is open
        ///     Pull request has "no-recent-activity" label
        ///     Pull request was last modified more than 7 days ago
        /// Resulting Action:
        ///     Close the pull request
        ///     Create a comment "Hi @${issueAuthor}.  Thank you for your contribution.  Since there hasn't been recent engagement, we're going to close this out.  Feel free to respond with a comment containing "/reopen" if you'd like to continue working on these changes.  Please be sure to use the command to reopen or remove the "no-recent-activity" label; otherwise, this is likely to be closed again with the next cleanup pass."
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task CloseStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseStalePullRequests))
            {

                List<string> includeLabels = new List<string>
                {
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 null,
                                                                 includeLabels);
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

                Console.WriteLine(result.TotalCount);
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is open
        ///     Issue has label "issue-addressed"
        ///     Issue was last updated more than 7 days ago
        /// Resulting Action:
        ///     Close the issue
        ///     Create a comment "Hi @${issueAuthor}, since you haven’t asked that we “`/unresolve`” the issue, we’ll close this out. If you believe further discussion is needed, please add a comment “`/unresolve`” to reopen the issue."
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task CloseAddressedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseAddressedIssues))
            {

                List<string> includeLabels = new List<string>
                {
                    LabelConstants.IssueAddressed
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 null,
                                                                 includeLabels);
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

                Console.WriteLine(result.TotalCount);
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is closed
        ///     Issue was last updated more than 90 days ago
        ///     Issue is unlocked
        /// Resulting Action:
        ///     Lock issue conversations
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        public static async Task LockClosedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.LockClosedIssues))
            {

                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(
                    scheduledEventPayload.Repository.Owner.Login,
                    scheduledEventPayload.Repository.Name,
                    IssueTypeQualifier.Issue,
                    ItemState.Closed,
                    90, // more than 90 days
                    new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });

                // Grab the first 100 issues
                SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                Console.WriteLine(result.TotalCount);
                for (int i = 0; i < result.TotalCount; i += 100)
                {
                    foreach (Issue issue in result.Items)
                    {
                        Console.WriteLine(issue.Number);
                        gitHubEventClient.LockIssue(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
                    }
                }
            }
        }
    }
}
