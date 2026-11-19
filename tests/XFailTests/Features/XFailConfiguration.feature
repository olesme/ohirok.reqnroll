Feature: XFail Configuration and Advanced Features

Scenario: Verbose output for xfail
    #xfail:ArgumentException
    Given step that throws ArgumentException
    Then step verifies verbose output was shown

Scenario: Verbose output for afail when step passes
    #afail
    Given step that succeeds
    Then step verifies afail success message shown

Scenario: Verbose output for afail when step fails
    #afail:InvalidOperationException
    Given step that throws InvalidOperationException
    Then step verifies afail failure message shown

Scenario: XFail preserves multiple hashtags
    #timeout:5000
    #retry:3
    #xfail
    Given step throws exception and saves hashtags to context
    Then saved hashtags contain:
      | hashtag      |
      | timeout:5000 |
      | retry:3      |
      | xfail        |
