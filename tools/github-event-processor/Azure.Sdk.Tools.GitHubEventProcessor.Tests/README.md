# GitHub Event Processor Tests

NUnit test project for GitHubEventProcessor. It contains a suite of static tests used to test the individual rules.

## Why are there only automated Static tests and no automated Live tests?

1. Actions requires something happening with an Issue, Pull_Request, Pull_Request_Review or Comment that's done through through the UI. Creating or modifying things authenticated using a token is not going to cause other events to fire. For example, adding a label to an issue, will not cause an **Issues Labeled** event to fire off.
2. Actions are repository specific. Even if we could do automated live testing, it would only able to be done a repository setup specifically for it which would not be the same repository that's building, packaging and releasing the tools package.
3. Rule specific reasons. There are rule specific reasons that would also make this challenging. For example, we have a rules that look for issues or pull_requests that haven't had activity for 7/14/60/90 days and those can't be created on the fly and as soon as one is modified by rules process, that activity counter resets.
4. Live testing really only ends up testing the GitHub API. Calling to get permissions for a user, checking a user's org, updating an Issue or Pull_Request, or adding a comment, none of these are really going to test anything related to the rules themselves that couldn't be tested with a static payload and mocked calls.


