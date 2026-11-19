Feature: Scenario health derived from run history

  Verifies Ohirok.Reporting.History.Sqlite.ReqnrollPlugin persists run outcomes correctly and that
  ReportHealthAnalyzer.Analyze - resolved and driven the same way OhirokReportFormatter.ApplyHistoryHealth
  does at report-generation time - derives the expected health label from them.

  Scenario: A scenario with no recorded runs is Unknown
    When I compute health for "NeverSeen" with a window of 4
    Then the health should be "Unknown"

  Scenario: A scenario with a single recorded run is New
    Given scenario "SingleRun" has recorded outcomes "Passed" oldest first
    When I compute health for "SingleRun" with a window of 4
    Then the health should be "New"

  Scenario: Two consecutive passes are Passing
    Given scenario "TwoPasses" has recorded outcomes "Passed,Passed" oldest first
    When I compute health for "TwoPasses" with a window of 4
    Then the health should be "Passing"

  Scenario: A pass followed by a failure is Regressed
    Given scenario "PassThenFail" has recorded outcomes "Passed,Failed" oldest first
    When I compute health for "PassThenFail" with a window of 4
    Then the health should be "Regressed"

  Scenario: Two consecutive failures are Broken
    Given scenario "TwoFails" has recorded outcomes "Failed,Failed" oldest first
    When I compute health for "TwoFails" with a window of 4
    Then the health should be "Broken"

  Scenario: A failure followed by a pass is Fixed
    Given scenario "FailThenPass" has recorded outcomes "Failed,Passed" oldest first
    When I compute health for "FailThenPass" with a window of 4
    Then the health should be "Fixed"

  Scenario: Alternating outcomes are Flaky
    Given scenario "Alternating" has recorded outcomes "Passed,Failed,Passed,Failed" oldest first
    When I compute health for "Alternating" with a window of 4
    Then the health should be "Flaky"

  Scenario: A most-recent run that was skipped/not invoked is Invalid
    Given scenario "LatestSkipped" has recorded outcomes "Passed,Passed,Incomplete" oldest first
    When I compute health for "LatestSkipped" with a window of 4
    Then the health should be "Invalid"

  # Regression test for the over-fetch fix: GetRecentRunsAsync's LIMIT is a raw row count applied before
  # Incomplete runs are filtered out, so fetching only `window` rows would leave just one genuine run
  # (Failed) once the two Incomplete rows above it are discarded - wrongly reporting New instead of
  # Regressed. OhirokReportFormatter over-fetches (window * 5) and caps ReportHealthAnalyzer.Analyze's
  # comparison to exactly `window` genuine runs, so skipped runs never shrink the comparison window.
  Scenario: Skipped runs interspersed above the window do not shrink the genuine-run comparison window
    Given scenario "SkipsAboveWindow" has recorded outcomes "Passed,Passed,Incomplete,Incomplete,Failed" oldest first
    When I compute health for "SkipsAboveWindow" with a window of 3
    Then the health should be "Regressed"
