Feature: All plugins co-installed smoke

  # Every plugin is installed at once here, and several of them resolve the same packages:
  # Ohirok.StepHashtags.ReqnrollPlugin is referenced directly AND arrives transitively through
  # Ohirok.XFail.ReqnrollPlugin, while all four share Ohirok.Reqnroll.PluginChains.Sdk. NuGet imports
  # each package's build props once per project no matter how many dependency paths reach it, so each
  # generator plugin must register exactly once. A double registration would show up as duplicated
  # generated code - which fails to compile - so this feature building at all is part of the check.

  @retry:2
  Scenario: Retry works alongside every other plugin
    When operation fails 2 times
    Then verify attempt count is 3

  Scenario: Step hashtags and expected failures coexist
    #timeout:5000
    #priority:high
    When I call the external service
    #xfail:InvalidOperationException
    When the known-bug step runs
    Then the xfail hook observed a failure

  Scenario: Soft-assert marker coexists with the other plugins
    #softassert
    Then the soft step is fine
    Then no soft failures were captured
