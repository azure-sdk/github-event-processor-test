# GitHub Event Processor

JRS - Not finished yet

## Overview

GitHub Event Processor is an Azure-SDK replacement for FabricBot. It's written in C# utilizing [Octokit.Net](https://github.com/octokit/octokit.net). Where FabricBot is a separate service, GitHub Event Processor will utilize Action and Scheduled events, triggered through [GitHub Action Workflows](https://docs.github.com/en/actions/using-workflows/about-workflows). These are defined YML files and placed into the .github/workflows directory of the repository utilizing them. For our purposes there will be two YML files, one for Actions and one for Scheduled events.

[Rules and Cron task definitions](./RULES.md)

## Events, Actions and YML

GitHubEventProcesses is invoked by the event-processor.yml file that lives in .github/workflow directory. The directory is special, GitHub will automatically process any yml file in this directory as a GitHub actions file. This yml file defines which events, and which actions on those events, we wish to process. *The full list events and their corresponding actions can be found [here](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows)*

For example:

```yml
on:
  # issues is the event
  issues:
    # these are the issues actions we're interested in processing, other issues
    # actions will not trigger processing from this yml file.
    types: [edited, labeled, opened, reopened, unlabeled]
  # issue_comment (includes PullRequest comments)
  issue_comment:
    # for comments, we only care to process when they're created
    types: [created]
```

This means that GitHub will only invoke the job in the yml file when an **issue** is edited, labeled, opened, reopened and unlabled or an **issue_comment** is created. All other events, and their actions, that aren't defined in the yml file will not trigger any processing.

### Command Line Arguments

If running an action:

```powershell
dotnet run -- ${{ github.event_name }} payload.json
```

If running a scheduled task:

```powershell
dotnet run -- ${{ github.event_name }} payload.json <TaskToRun>
```

**github.event_name** will be one of the [workflow trigger events](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows). These are things like issues, issue_comment, pull_request_target, pull_request_review etc. This option is how the application knows what to class to deserialize the event payload into.

**payload.json** is the toJson of the github.event redirected into file. The action that triggered the event is part of this payload.

**TaskToRun** is specific to Scheduled event processing and defines what rule to run. This string matches the rule name constant defined in the [RulesConstants](./Constants/RulesConstants.cs) file. The reason this was done this way is that it prevents the code from needing knowledge of which cron schedule string belongs to which rule.

### Rules Configuration

The [rules configuration file](../yml-files/event-processor.config) is simply a Json file which defines which rules are active for the repository and they're loaded up every time the GitHubEventProcessor runs. The full set rules is in the [RulesConstants](./Constants/RulesConstants.cs) file and their state is either **On** or **Off**. *Note: AzureSdk language repositories should have all rules enabled but non-language repositories, like azure-sdk-tools, have a reduced set of rules. For example:

```json
  "InitialIssueTriage": "On",
  "ManualIssueTriage": "On",
  "ServiceAttention": "Off",
```

All three of the above rules are *Issues* event rules. InitialIssueTriage and ManualIssueTriage would both run because they're **On** but ServiceAttention would not because it's **Off**. Also, just because a rule is **On** doesn't mean it'll always make updates to an Issue.

Every rule has a the following definition:

- **Trigger** - The event and action that will cause a given rule to process.
- **Criteria** - A set of evaluations performed for a given trigger that will determine what, if any, action to take.
- **Actions** - A set of things to happen in response to a trigger based upon the criteria.

For example, **ManualIssueTriage** is a rule that will only processes on an **Issues** event's **labeled** action. Its criteria is that the issue must be *Open*, have the *needs-triage* label and the label being added is not *needs-triage*. The action to take, if all the criteria has been met, is to remove the *needs-triage* label from the issue.

The full list of Rules and their definitions can be found [here](../RULES.md)

## Octokit.Net
